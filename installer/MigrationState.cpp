// MIT-licensed, original WinThunar installer custom actions. No application runtime required.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <msi.h>
#include <msiquery.h>
#include <shlobj.h>
#include <tlhelp32.h>
#include <string>
#include <stdexcept>
#include <cwctype>

namespace {
constexpr auto StateName = L"\\WinThunar";
constexpr auto HeldName = L"\\WinThunar.MsiMigrationState";

struct Handle {
    HANDLE value;
    ~Handle() { if (value != INVALID_HANDLE_VALUE && value != nullptr) CloseHandle(value); }
};

std::wstring Property(MSIHANDLE session, const wchar_t* name) {
    DWORD size = 0;
    wchar_t empty = 0;
    UINT result = MsiGetPropertyW(session, name, &empty, &size);
    if (result != ERROR_SUCCESS && result != ERROR_MORE_DATA) throw std::runtime_error("Read MSI property");
    std::wstring value(size + 1, L'\0');
    DWORD capacity = size + 1;
    if (MsiGetPropertyW(session, name, value.data(), &capacity) != ERROR_SUCCESS)
        throw std::runtime_error("Read MSI property");
    value.resize(capacity);
    return value;
}

void Log(MSIHANDLE session, const std::wstring& message, bool error = false) {
    MSIHANDLE record = MsiCreateRecord(0);
    if (!record) return;
    MsiRecordSetStringW(record, 0, (L"WinThunar migration: " + message).c_str());
    MsiProcessMessage(session, error ? INSTALLMESSAGE_ERROR : INSTALLMESSAGE_INFO, record);
    MsiCloseHandle(record);
}

DWORD Attributes(const std::wstring& path) {
    DWORD attributes = GetFileAttributesW(path.c_str());
    if (attributes == INVALID_FILE_ATTRIBUTES) {
        DWORD error = GetLastError();
        if (error != ERROR_FILE_NOT_FOUND && error != ERROR_PATH_NOT_FOUND)
            throw std::runtime_error("Cannot inspect migration state");
    }
    return attributes;
}

bool DirectoryPresent(const std::wstring& path) {
    DWORD attributes = Attributes(path);
    if (attributes == INVALID_FILE_ATTRIBUTES) return false;
    if (!(attributes & FILE_ATTRIBUTE_DIRECTORY) || (attributes & FILE_ATTRIBUTE_REPARSE_POINT))
        throw std::runtime_error("State or holding path is not an ordinary directory");
    return true;
}

// Rename the directory itself on the same volume: no traversal, copying, replacement or
// recursive deletion. NTFS keeps security descriptors, streams, links, timestamps and bytes.
void Rename(const std::wstring& source, const std::wstring& destination) {
    Handle directory{CreateFileW(source.c_str(), DELETE | FILE_READ_ATTRIBUTES,
        FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING,
        FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, nullptr)};
    if (directory.value == INVALID_HANDLE_VALUE) throw std::runtime_error("Cannot open state for rename; close WinThunar and its updater");
    BY_HANDLE_FILE_INFORMATION info{};
    if (!GetFileInformationByHandle(directory.value, &info) ||
        !(info.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) || (info.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT))
        throw std::runtime_error("State directory changed or is a reparse point");
    const DWORD bytes = static_cast<DWORD>(destination.size() * sizeof(wchar_t));
    std::wstring storage((sizeof(FILE_RENAME_INFO) + bytes + sizeof(wchar_t) - 1) / sizeof(wchar_t), L'\0');
    auto rename = reinterpret_cast<FILE_RENAME_INFO*>(storage.data());
    rename->ReplaceIfExists = FALSE;
    rename->RootDirectory = nullptr;
    rename->FileNameLength = bytes;
    memcpy(rename->FileName, destination.data(), bytes);
    if (!SetFileInformationByHandle(directory.value, FileRenameInfo, rename,
        static_cast<DWORD>(sizeof(FILE_RENAME_INFO) + bytes)))
        throw std::runtime_error("State rename failed; preserved data has not been overwritten");
}

void Protect(const std::wstring& parent) {
    const auto state = parent + StateName;
    const auto held = parent + HeldName;
    if (Attributes(held) != INVALID_FILE_ATTRIBUTES)
        throw std::runtime_error("A previous migration holding directory exists; recover it before retrying");
    if (DirectoryPresent(state)) Rename(state, held);
}

void Restore(const std::wstring& parent) {
    const auto state = parent + StateName;
    const auto held = parent + HeldName;
    if (!DirectoryPresent(held)) return; // No original state, or already restored.
    if (DirectoryPresent(state)) {
        // Old rollback may recreate an empty directory. Never remove its contents or merge
        // competing files. RemoveDirectory only succeeds for an empty, ordinary directory.
        if (!RemoveDirectoryW(state.c_str()))
            throw std::runtime_error("State was recreated and is not empty; holding directory retained for recovery");
    }
    Rename(held, state);
}

bool LegacyProduct(const std::wstring& product) {
    DWORD length = 0;
    UINT result = MsiGetProductInfoW(product.c_str(), INSTALLPROPERTY_LOCALPACKAGE, nullptr, &length);
    if (result != ERROR_SUCCESS && result != ERROR_MORE_DATA) throw std::runtime_error("Cannot inspect related MSI cache");
    std::wstring cache(length + 1, L'\0');
    ++length;
    if (MsiGetProductInfoW(product.c_str(), INSTALLPROPERTY_LOCALPACKAGE, cache.data(), &length) != ERROR_SUCCESS)
        throw std::runtime_error("Cannot inspect related MSI cache");
    MSIHANDLE database = 0, view = 0, record = 0;
    bool legacy = false;
    result = MsiOpenDatabaseW(cache.c_str(), MSIDBOPEN_READONLY, &database);
    if (result == ERROR_SUCCESS)
        result = MsiDatabaseOpenViewW(database, L"SELECT `Value` FROM `Property` WHERE `Property` = 'RustAppId'", &view);
    if (result == ERROR_SUCCESS) result = MsiViewExecute(view, 0);
    if (result == ERROR_SUCCESS) result = MsiViewFetch(view, &record);
    if (result == ERROR_SUCCESS) {
        wchar_t value[64]{};
        DWORD size = 64;
        result = MsiRecordGetStringW(record, 1, value, &size);
        if (result == ERROR_SUCCESS) {
            legacy = wcscmp(value, L"WinThunar") == 0;
            if (!legacy && wcscmp(value, L"WinThunar.App") != 0) result = ERROR_INVALID_DATA;
        }
    }
    if (record) MsiCloseHandle(record);
    if (view) { MsiViewClose(view); MsiCloseHandle(view); }
    if (database) MsiCloseHandle(database);
    if (result != ERROR_SUCCESS) throw std::runtime_error("Unrecognized related MSI cleanup policy; migration stopped");
    return legacy;
}

void RequireClosedApplication() {
    Handle snapshot{CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)};
    if (snapshot.value == INVALID_HANDLE_VALUE) throw std::runtime_error("Cannot check running applications");
    PROCESSENTRY32W entry{};
    entry.dwSize = sizeof(entry);
    if (!Process32FirstW(snapshot.value, &entry)) throw std::runtime_error("Cannot check running applications");
    do {
        if (_wcsicmp(entry.szExeFile, L"WinThunar.exe") == 0)
            throw std::runtime_error("Close all WinThunar windows before migrating the older MSI");
        if (_wcsicmp(entry.szExeFile, L"Update.exe") == 0) {
            Handle process{OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, entry.th32ProcessID)};
            wchar_t path[32768]{};
            DWORD length = 32768;
            if (process.value == nullptr || !QueryFullProcessImageNameW(process.value, 0, path, &length))
                throw std::runtime_error("Wait for the running updater to finish before migration");
            std::wstring executable(path);
            for (auto& ch : executable) ch = static_cast<wchar_t>(towlower(ch));
            if (executable.find(L"\\winthunar.app\\") != std::wstring::npos)
                throw std::runtime_error("Wait for the WinThunar updater to finish before migration");
        }
    } while (Process32NextW(snapshot.value, &entry));
    if (GetLastError() != ERROR_NO_MORE_FILES) throw std::runtime_error("Incomplete application process check");
}

void RequireElevatedMigration() {
    // The unelevated MSI engine can restore files yet fail to restore its own product
    // registration during rollback. Check the effective token, not caller-settable MSI
    // properties such as Privileged or MsiRunningElevated. Never change MSI registry ACLs.
    HANDLE value = nullptr;
    if (!OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, &value)) {
        if (GetLastError() != ERROR_NO_TOKEN ||
            !OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &value))
            throw std::runtime_error("Cannot verify migration privileges; installation stopped before removal");
    }
    Handle token{value};
    TOKEN_ELEVATION elevation{};
    DWORD bytes = 0;
    if (!GetTokenInformation(token.value, TokenElevation, &elevation, sizeof(elevation), &bytes))
        throw std::runtime_error("Cannot verify migration privileges; installation stopped before removal");
    if (!elevation.TokenIsElevated)
        throw std::runtime_error("Migrating this older Just me MSI requires administrator approval for safe installer rollback. Close this installer, run it as administrator using the same Windows account, and select Just me. Do not switch to another administrator account");
}

UINT Failure(MSIHANDLE session, const std::exception& exception) {
    std::string detail(exception.what());
    Log(session, std::wstring(detail.begin(), detail.end()) +
        L". Original data, if held, remains in %LOCALAPPDATA%\\WinThunar.MsiMigrationState. "
        L"Do not delete it. Close WinThunar and consult the MSI migration instructions.", true);
    return ERROR_INSTALL_FAILURE;
}
}

extern "C" __declspec(dllexport) UINT __stdcall PrepareMigration(MSIHANDLE session) {
    try {
        const auto products = Property(session, L"WIX_UPGRADE_DETECTED");
        bool legacy = false;
        size_t start = 0;
        while (start < products.size()) {
            size_t end = products.find(L';', start);
            const auto product = products.substr(start, end == std::wstring::npos ? end : end - start);
            if (product.size() != 38 || product.front() != L'{' || product.back() != L'}')
                throw std::runtime_error("Invalid related product identity");
            legacy = LegacyProduct(product) || legacy;
            if (end == std::wstring::npos) break;
            start = end + 1;
        }
        if (!legacy) return ERROR_SUCCESS;
        // The legacy machine cleanup runs as SYSTEM and is not restricted to the initiating
        // user's profile. Refuse that unverified path before any mutation.
        if (Property(session, L"ALLUSERS") == L"1")
            throw std::runtime_error("Automatic legacy-MSI migration currently supports Just me only; all-users migration requires a verified external state backup");
        if (Property(session, L"RollbackDisabled") == L"1" || Property(session, L"DISABLEROLLBACK") == L"1")
            throw std::runtime_error("Legacy migration requires Windows Installer rollback enabled");
        RequireElevatedMigration();
        RequireClosedApplication();
        PWSTR folder = nullptr;
        if (FAILED(SHGetKnownFolderPath(FOLDERID_LocalAppData, KF_FLAG_DONT_VERIFY, nullptr, &folder)))
            throw std::runtime_error("Cannot resolve the current user's LocalAppData");
        std::wstring parent(folder);
        CoTaskMemFree(folder);
        if (!DirectoryPresent(parent)) throw std::runtime_error("LocalAppData is unavailable");
        if (Attributes(parent + HeldName) != INVALID_FILE_ATTRIBUTES)
            throw std::runtime_error("Previous migration holding directory exists; recover it before retrying");
        DirectoryPresent(parent + StateName);
        for (auto action : {L"WinThunarProtectState", L"WinThunarRestoreState", L"WinThunarRollbackState"})
            if (MsiSetPropertyW(session, action, parent.c_str()) != ERROR_SUCCESS)
                throw std::runtime_error("Cannot prepare migration action data");
        if (MsiSetPropertyW(session, L"WinThunarMigrationNeeded", L"1") != ERROR_SUCCESS)
            throw std::runtime_error("Cannot enable migration protection");
        Log(session, L"Legacy per-user MSI detected. State protection scheduled before removal.");
        return ERROR_SUCCESS;
    } catch (const std::exception& exception) { return Failure(session, exception); }
}

extern "C" __declspec(dllexport) UINT __stdcall ProtectState(MSIHANDLE session) {
    try {
        RequireClosedApplication();
        Protect(Property(session, L"CustomActionData"));
        Log(session, L"State directory held outside legacy cleanup.");
        return ERROR_SUCCESS;
    } catch (const std::exception& exception) { return Failure(session, exception); }
}

extern "C" __declspec(dllexport) UINT __stdcall RestoreState(MSIHANDLE session) {
    try {
        Restore(Property(session, L"CustomActionData"));
        Log(session, L"State directory restored (success or rollback).");
        return ERROR_SUCCESS;
    } catch (const std::exception& exception) { return Failure(session, exception); }
}
