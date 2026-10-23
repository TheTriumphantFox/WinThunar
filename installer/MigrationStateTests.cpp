// Exercises real NTFS renames, locks and alternate streams without installing an MSI.
#include "MigrationState.cpp"
#include <iostream>
#include <filesystem>
#include <fstream>

namespace fs = std::filesystem;
void Check(bool passed, const char* name) {
    if (!passed) throw std::runtime_error(name);
    std::cout << "Passed: " << name << std::endl;
}
template<typename Operation> void Reject(Operation operation, const char* name) {
    bool rejected = false;
    try { operation(); } catch (const std::exception&) { rejected = true; }
    Check(rejected, name);
}
BY_HANDLE_FILE_INFORMATION Identity(const std::wstring& path) {
    Handle file{CreateFileW(path.c_str(), FILE_READ_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, nullptr)};
    BY_HANDLE_FILE_INFORMATION info{};
    if (file.value == INVALID_HANDLE_VALUE || !GetFileInformationByHandle(file.value, &info))
        throw std::runtime_error("Cannot read fixture identity");
    return info;
}
int wmain(int argc, wchar_t** argv) {
    try {
        if (argc == 4 && wcscmp(argv[1], L"--preflight") == 0) {
            // Read-only native MSI session: call the actual preflight function directly.
            // OpenPackage's automation DoAction cannot execute this action outside a sequence.
            MsiSetInternalUI(INSTALLUILEVEL_NONE, nullptr);
            MSIHANDLE session = 0;
            if (MsiOpenPackageExW(argv[2], MSIOPENPACKAGEFLAGS_IGNOREMACHINESTATE, &session) != ERROR_SUCCESS)
                throw std::runtime_error("Cannot open candidate for preflight checks");
            Check(LegacyProduct(argv[3]), "Actual installed cache is a legacy cleanup MSI");
            MsiSetPropertyW(session, L"WIX_UPGRADE_DETECTED", argv[3]);
            MsiSetPropertyW(session, L"ALLUSERS", L"1");
            Check(PrepareMigration(session) == ERROR_INSTALL_FAILURE && Property(session, L"WinThunarMigrationNeeded").empty(),
                "All-users legacy migration refused before scheduling changes");
            MsiSetPropertyW(session, L"ALLUSERS", L"");
            MsiSetPropertyW(session, L"DISABLEROLLBACK", L"1");
            Check(PrepareMigration(session) == ERROR_INSTALL_FAILURE && Property(session, L"WinThunarMigrationNeeded").empty(),
                "Rollback-disabled legacy migration refused before scheduling changes");
            MsiSetPropertyW(session, L"DISABLEROLLBACK", L"");
            // The guest regression runs this probe unelevated. Forged MSI properties must
            // not let the unsafe legacy removal begin or schedule a state-directory rename.
            MsiSetPropertyW(session, L"Privileged", L"1");
            MsiSetPropertyW(session, L"MsiRunningElevated", L"1");
            Check(PrepareMigration(session) == ERROR_INSTALL_FAILURE && Property(session, L"WinThunarMigrationNeeded").empty(),
                "Unelevated legacy migration refused despite caller-supplied privilege properties");
            MsiCloseHandle(session);
            return 0;
        }
        if (argc != 2) throw std::runtime_error("Supply a new test directory");
        auto root = fs::absolute(argv[1]);
        if (fs::exists(root)) throw std::runtime_error("Test directory already exists; preserve earlier evidence");
        fs::create_directories(root);
        const auto parent = root.wstring();
        const auto state = parent + StateName;
        const auto held = parent + HeldName;
        Protect(parent); Restore(parent);
        Check(!fs::exists(held) && !fs::exists(state), "Absent state remains absent");
        fs::create_directories(fs::path(state) / L"TransferRecovery" / L"empty");
        const auto file = state + L"\\TransferRecovery\\retained-original.bin";
        std::ofstream(fs::path(file)) << "original retained bytes";
        std::ofstream(fs::path(file + L":migration-stream")) << "alternate retained bytes";
        const auto before = Identity(file);
        const auto directoryBefore = Identity(state);
        Protect(parent);
        Check(!fs::exists(state) && fs::exists(held), "State held outside legacy cleanup");
        Reject([&] { Protect(parent); }, "Stale holding directory stops another protection attempt");
        Restore(parent);
        const auto after = Identity(file);
        const auto directoryAfter = Identity(state);
        Check(before.nFileIndexHigh == after.nFileIndexHigh && before.nFileIndexLow == after.nFileIndexLow &&
            before.dwVolumeSerialNumber == after.dwVolumeSerialNumber &&
            directoryBefore.nFileIndexHigh == directoryAfter.nFileIndexHigh && directoryBefore.nFileIndexLow == directoryAfter.nFileIndexLow,
            "Directory and retained file identities survive; no copy replaces originals");
        std::string stream;
        std::getline(std::ifstream(fs::path(file + L":migration-stream")), stream);
        Check(stream == "alternate retained bytes", "Alternate data stream retained");
        Check(fs::is_directory(fs::path(state) / L"TransferRecovery" / L"empty"), "Empty recovery directories retained");
        Restore(parent);
        Check(fs::exists(file) && !fs::exists(held), "Repeated rollback after restore is harmless");
        Protect(parent);
        fs::create_directory(state);
        Restore(parent);
        Check(fs::exists(file) && !fs::exists(held), "Rollback can replace an empty recreated directory");
        Protect(parent);
        fs::create_directory(state);
        std::ofstream(fs::path(state) / L"competing.json") << "new competing state";
        Reject([&] { Restore(parent); }, "Restore refuses competing files");
        Check(fs::exists(fs::path(state) / L"competing.json") && fs::exists(held + L"\\TransferRecovery\\retained-original.bin"),
            "Both original and competing data survive failed restore");
        fs::remove(fs::path(state) / L"competing.json"); // Exact fixture file only.
        Restore(parent);
        {
            Handle lock{CreateFileW(state.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, nullptr)};
            Check(lock.value != INVALID_HANDLE_VALUE, "Directory lock fixture opened");
            Reject([&] { Protect(parent); }, "Sharing lock rejects migration before removal");
            Check(fs::exists(file) && !fs::exists(held), "Locked migration leaves original state in place");
        }
        const auto external = (root / L"external").wstring();
        fs::create_directory(external);
        const auto linkParent = root / L"link-case";
        fs::create_directory(linkParent);
        if (CreateSymbolicLinkW((linkParent.wstring() + StateName).c_str(), external.c_str(),
            SYMBOLIC_LINK_FLAG_DIRECTORY | SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE)) {
            Reject([&] { Protect(linkParent.wstring()); }, "Reparse state root rejected without following its target");
        } else std::cout << "Skipped: symbolic-link case requires privilege" << std::endl;
        std::cout << "Native migration state checks passed; evidence retained at supplied directory." << std::endl;
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << std::endl;
        return 1;
    }
}
