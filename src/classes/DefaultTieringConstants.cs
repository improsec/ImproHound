namespace ImproHound.classes
{
    public static class DefaultTieringConstants
    {

        public static string[] tier2GroupRids = {
            "-545", // Users
            "-546", // Guests
            "-554", // Pre-Windows 2000 Compatible Access
            "-1-0"  // Everyone (S-1-1-0)
        };

        public static string[] tier0GroupRids = {
            "-498", // Enterprise Read-only Domain Controllers
            "-512", // Domain Admins
            "-516", // Domain Controllers
            "-517", // Cert Publishers
            "-518", // Schema Admins
            "-519", // Enterprise Admins
            "-520", // Group Policy Creator Owners
            "-521", // Read-only Domain Controllers
            "-522", // Cloneable Domain Controllers
            "-526", // Key Admins
            "-527", // Enterprise Key Admins
            "-544", // Administrators
            "-547", // Power Users
            "-548", // Account Operators
            "-549", // Server Operators
            "-550", // Print Operators
            "-551", // Backup Operators
            "-552", // Replicators
            "-555", // Remote Desktop Users
            "-556", // Network Configuration Operators
            "-557", // Incoming Forest Trust Builders
            "-558", // Performance Monitor Users
            "-559", // Performance Log Users
            "-560", // Windows Authorization Access Group
            "-561", // Terminal Server License Servers
            "-562", // Distributed COM Users
            "-569", // Cryptographic Operators
            "-573", // Event Log Readers
            "-574", // Certificate Service DCOM Access 
            "-578", // Hyper-V Administrators
            "-579", // Access Control Assistance Operators
            "-580", // Remote Management Users
            "-582", // Storage Replica Administrators
        };

        public static string tier0DnsAdmins = "CN=DnsAdmins,";
        public static string tier0WinRMRemoteWMIUsers__ = "CN=WinRMRemoteWMIUsers__,";

        public static string[] tier0UserRids = {
            "-500", // Administrator
            "-502"  // KRBTGT
        };
    }
}
