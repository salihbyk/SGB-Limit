# Image provenance

The images show SGB Limit's actual native WPF interface with synthetic quotas from `MediaRenderer.Demo`. No real account screenshots, authentication files or user settings are included.

`hero.png` and `vertical.png` combine native component captures over an original OpenAI ImageGen background. Labels are deterministic text. UI text and values were not invented or redrawn by an image model. Components are enlarged for readability; these compositions do not claim all presentation modes are active simultaneously.

The raw desktop scenes were captured from shown native windows and the embedded taskbar widget on a temporary wallpaper presentation surface. This surface covers desktop icons and open documents without changing wallpaper, icon settings or user windows. Capturing runs before account discovery and makes no quota requests.

To reproduce native scenes on Windows:

```powershell
.\SGBLimit.exe --capture-presentation C:\captures C:\wallpaper.png
```

Exit any normally running SGB Limit first to avoid overlapping its real widget with the synthetic capture. Capture windows ignore input and settings writes are isolated to the output folder. The current desktop/taskbar will be present in raw captures, so inspect them for personal content before publishing. Desktop captures were verified at 96 DPI; higher DPI uses physical primary-monitor pixel bounds but has not been visually verified on another display.

Background prompt: a minimal 16:9 desktop wallpaper with dark basalt folds and quiet mountain silhouettes, charcoal and desaturated sage, muted ivory light at lower right, empty upper area, no words, logos, UI, people or icons. Generated with the built-in OpenAI ImageGen tool; code preserves the exact native interface. Rights available to the repository owner are offered under LICENSE without a claim of exclusive copyright in generated material.
