Pulse - Digital Wellbeing PC — MSIX tile / logo assets
=======================================================

The PNG assets in this folder are referenced by ..\Package.appxmanifest and are
GENERATED from the master icon — do not hand-edit them.

MASTER
------
    ..\..\digital-wellbeing-app\Resources\Icons\digital-balance-icon-1024.png
    (1024x1024, upscaled from the 256px app icon. All tiles render at <=310px, so
    they downscale crisply. Replace with higher-res / vector-derived art for the
    sharpest result, then regenerate.)

REGENERATE
----------
    pwsh ..\scripts\generate-store-assets.ps1
    (or pass -Master <path> to use a different master)

GENERATED FILES
---------------
    StoreLogo.png ............ 50x50    (Properties\Logo, Store listing)
    Square44x44Logo.png ...... 44x44    (app list, taskbar) + scale-200 + targetsize 16/24/32/48/256
    Square71x71Logo.png ...... 71x71    (SmallTile)
    Square150x150Logo.png .... 150x150  (medium tile) + scale-200
    LargeTile.png ............ 310x310  (Square310x310Logo)
    Wide310x150Logo.png ...... 310x150  (wide tile)
    SplashScreen.png ......... 620x300  (splash screen)
