// File: Services/PackagedAppInfo.cs
using System.Runtime.InteropServices;
using System.Text;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Detects whether the app is running with MSIX package identity (Microsoft Store
    /// build) and exposes Store-related constants/deep links.
    ///
    /// Identity is probed via the Win32 <c>GetCurrentPackageFullName</c> API rather than
    /// <c>Windows.ApplicationModel.Package.Current</c>, because the WinRT accessor throws
    /// on unpackaged builds. This class is exception-free: it interprets the documented
    /// APPMODEL_ERROR_NO_PACKAGE (15700) result as "not packaged".
    /// </summary>
    public static class PackagedAppInfo
    {
        /// <summary>
        /// Microsoft Store product (Big) ID for "Pulse - Digital Wellbeing PC".
        /// </summary>
        public const string StoreProductId = "9ND5GNMBQVQ7";

        /// <summary>
        /// Package Family Name assigned by the Store for this identity.
        /// </summary>
        public const string PackageFamilyName = "SwarajDhondge.DigitalWellbeingPC_h295br46hdej4";

        // ERROR_SUCCESS: a package full name was written to the buffer.
        private const int ErrorSuccess = 0;
        // ERROR_INSUFFICIENT_BUFFER: buffer too small, but identity DOES exist (packaged).
        private const int ErrorInsufficientBuffer = 122;
        // APPMODEL_ERROR_NO_PACKAGE: process has no package identity (unpackaged / not Store).
        private const int AppModelErrorNoPackage = 15700;

        private static readonly bool _isPackaged = DetectPackaged();

        /// <summary>
        /// True when the process has MSIX package identity (i.e. installed from the Store
        /// or a sideloaded MSIX). False for the classic/Velopack build. Never throws.
        /// </summary>
        public static bool IsPackaged => _isPackaged;

        /// <summary>
        /// Store deep link that opens the product detail page for this app.
        /// Launch with <c>Process.Start(new ProcessStartInfo(GetStoreDeepLink()){ UseShellExecute = true })</c>.
        /// </summary>
        public static string GetStoreDeepLink()
            => $"ms-windows-store://pdp/?productid={StoreProductId}";

        private static bool DetectPackaged()
        {
            try
            {
                int length = 0;
                // First call: length=0 probes the required buffer size / identity presence.
                int rc = GetCurrentPackageFullName(ref length, null);

                if (rc == AppModelErrorNoPackage)
                    return false;

                // Any success or "buffer too small" outcome means identity exists.
                if (rc == ErrorSuccess || rc == ErrorInsufficientBuffer)
                    return true;

                // Unexpected code: fill an actual buffer and re-check defensively.
                if (length > 0)
                {
                    var buffer = new StringBuilder(length);
                    rc = GetCurrentPackageFullName(ref length, buffer);
                    return rc == ErrorSuccess;
                }

                return false;
            }
            catch (EntryPointNotFoundException)
            {
                // API unavailable (pre-Win8 / unusual host): treat as unpackaged.
                return false;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
        }

        // https://learn.microsoft.com/windows/win32/api/appmodel/nf-appmodel-getcurrentpackagefullname
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern int GetCurrentPackageFullName(
            ref int packageFullNameLength,
            StringBuilder? packageFullName);
    }
}
