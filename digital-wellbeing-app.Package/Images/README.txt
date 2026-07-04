Pulse - Digital Wellbeing PC — MSIX tile / logo assets
=======================================================

This folder must contain the PNG assets referenced by ..\Package.appxmanifest.
They are NOT checked in yet because a suitable master image does not exist:
the largest existing icon is only 256x256, which is too small for the 310x310
LargeTile and the scale-200 variants.

TO PRODUCE THE REAL ASSETS
--------------------------
1. Create a square, transparent-background master PNG of AT LEAST 512x512
   (ideally 1024x1024) at:
       ..\..\digital-wellbeing-app\Resources\Icons\digital-balance-icon-512.png
2. Run:
       pwsh ..\scripts\generate-store-assets.ps1
   (or pass -Master <path> to use a master elsewhere).

REQUIRED FILES (base scale-100 sizes; see the script for scaled/targetsize variants)
------------------------------------------------------------------------------------
    StoreLogo.png ............ 50x50    (Properties\Logo, Store listing)
    Square44x44Logo.png ...... 44x44    (app list, taskbar)
    Square71x71Logo.png ...... 71x71    (SmallTile)
    Square150x150Logo.png .... 150x150  (medium tile)
    LargeTile.png ............ 310x310  (Square310x310Logo)
    Wide310x150Logo.png ...... 310x150  (wide tile)
    SplashScreen.png ......... 620x300  (splash screen)

Until these exist, the .wapproj will build the manifest against missing paths
and packaging/WACK will fail — generate them first.
