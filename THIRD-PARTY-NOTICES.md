# Credits and third-party notices

| Component | Version / source | Attribution / license |
|---|---|---|
| dsh-550c-boot | 0.1.3, bddc507d7c717fe7cda440cec83f330e8b3e5004 | Copyright 2026 Ziyang Song, MIT; original animation HTML by Voidpoket |
| 550W Controller adaptations | 3.0.0 | 550W AI Controller contributors, MIT |
| V3 mechanical core PNG | Original AI-generated project illustration based on user-provided visual direction | Built-in image generation; no film image asset; prompt/provenance in docs/core-artwork.md |
| Legacy mechanical core SVG | Original V2 vector artwork retained as source | MIT |
| Default synthesized WAVs | Original project syntheses | MIT; no soundtrack or actor recording |
| .NET / Windows Desktop runtime | 8.0.31, win-x64 | Microsoft/.NET Foundation licenses and third-party notices in licenses/ |
| System.Management / System.CodeDom | 8.0.0 | Microsoft, MIT notices in licenses/ |
| Microsoft.Web.WebView2 SDK binaries | 1.0.4258.31 | Microsoft SDK license and NOTICE in licenses/ |
| Windows SDK .NET reference package | 10.0.17763.56 | Microsoft SDK license in licenses/Windows-SDK-License.rtf |
| WebView2 Evergreen Runtime | Installed separately by user | Microsoft Runtime terms; Runtime is not included in this archive |
| Windows SAPI voice engine | User's installed Windows components | No voice library redistributed by this project |
| eSpeak NG offline voice engine/data | 1.52.0, 4870adfa25b1a32b4361592f1be8a40337c58d6c | eSpeak NG contributors, GPL-3.0-or-later; COPYING and complete source in VoiceEngines/espeak-ng/ |
| 550W.VoiceWorker | Original standalone synthesis worker | GPL-3.0-or-later; source and build script accompany binary |
| eSpeak binary distribution / build scripts | espeakng-loader 0.2.4 by thewh1teagle | Loader scripts MIT, bundled eSpeak engine retains GPL; Python wrapper is not used or shipped |
| xUnit / Microsoft.NET.Test.Sdk | 2.9.3 / 17.12.0 | Development dependencies only, restored from NuGet |
| Playwright | 1.62.1 | Development dependency only, restored with npm |

The complete unmodified upstream source and its license are included in the source archive. Runtime packages retain the copied vendor license files. The application does not distribute official ChatGPT, DeepSeek, Claude or other client executables, icons copied out of a user's installation, account files or credentials. Application icons are resolved and cached locally when a user creates an optimized shortcut.

The eSpeak NG engine and voice data were extracted unchanged from the win_amd64 espeakng-loader 0.2.4 wheel. The pinned official engine source and distributor build workflow (including voice-data compilation) are supplied in VoiceEngines/espeak-ng/ along with the worker source, GPL text and provenance checksums. See VOICE-ENGINE.txt there. No Microsoft installed system-voice files are redistributed.

MIT code permission does not grant rights to third-party film recordings or trademarks. Users are responsible for rights to any locally substituted media they choose to redistribute.
