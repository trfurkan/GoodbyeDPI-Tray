# Third-party components

GoodbyeDPI Tray is an independent native Windows GUI by [Furkan / trfurkan](https://github.com/trfurkan).
The original application code is covered by the root [Apache 2.0 license](LICENSE).
The components below retain their own copyright notices and licenses.

| Component | Origin / source | License supplied with the bundle |
| --- | --- | --- |
| GoodbyeDPI 0.2.3rc3 motor | [ValdikSS/GoodbyeDPI](https://github.com/ValdikSS/GoodbyeDPI) | [Apache 2.0](vendor/goodbyedpi/licenses/LICENSE-goodbyedpi.txt) |
| Turkey profile scripts and distribution | [cagritaskn/GoodbyeDPI-Turkey](https://github.com/cagritaskn/GoodbyeDPI-Turkey) | Original distribution notices and scripts retained in `vendor/goodbyedpi/` |
| WinDivert DLLs and drivers | [basil00/WinDivert](https://github.com/basil00/WinDivert) | [LGPL v3 text supplied with the distribution](vendor/goodbyedpi/licenses/LICENSE-windivert.txt) |
| uthash used by the motor | [troydhanson/uthash](https://github.com/troydhanson/uthash) | [Original copyright and redistribution terms](vendor/goodbyedpi/licenses/LICENSE-uthash.txt) |
| getline used by the motor | NetBSD Foundation / Christos Zoulas | [Original BSD terms](vendor/goodbyedpi/licenses/LICENSE-getline.txt) |

## Bundled files

`vendor/goodbyedpi/` contains the binary distribution originally supplied as
`goodbyedpi-0.2.3rc3-turkey`: the unmodified motor, WinDivert files, license
texts and the `turkey_dnsredir*.cmd` profile scripts. The original
`service_*.cmd` scripts were removed because this app manages its own service.
The motor and WinDivert binaries are packaged into the installer without
recompiling or modifying them. Our service host runs the motor as a separate
process with the selected original profile arguments.

Upstream binary distribution: [GoodbyeDPI-Turkey releases](https://github.com/cagritaskn/GoodbyeDPI-Turkey/releases).
Motor source and build instructions: [GoodbyeDPI](https://github.com/ValdikSS/GoodbyeDPI).
WinDivert source, releases and build instructions: [WinDivert](https://github.com/basil00/WinDivert).

The root Apache license applies to our original code; it does not relicense
WinDivert or other third-party components. Keep their license and copyright
notices when redistributing the files. The original distribution's bundled
notices are authoritative for those files.
