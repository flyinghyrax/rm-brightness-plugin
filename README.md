## ScreenBrightness.dll ##

This is a plugin for [Rainmeter][rm] which measures screen backlight brightness and supports bangs to manipulate the brightness level.  It uses the [Rainmeter Plugin SDK][pluginsdk] and the [Windows Management Interface][msMonitor].

[rm]: http://rainmeter.net/
[pluginsdk]: https://github.com/rainmeter/rainmeter-plugin-sdk
[msMonitor]: http://msdn.microsoft.com/en-us/library/aa392707%28v=vs.85%29.aspx

### Requires #
- Hardware support (obviously)
- Windows Vista or higher

### Usage #
The plugin measure:
```INI
[measureBrightness]
Measure=Plugin
Plugin=ScreenBrightness.dll
UpdateDivider=5
```
Bang syntax:

- `!CommandMeasure "measureBrightness" "raise"` -- raises brightness by one level
- `!CommandMeasure "measureBrightness" "lower"` -- lower brightness by one level
- `!CommandMeasure "measureBrightness" "set N"` -- sets brightness to the supported level closest to the number `N`

Complete sample skin:
```INI
[Rainmeter]

[Metadata]

[Variables]

[measureBrightness]
Measure=Plugin
Plugin=ScreenBrightness.dll
UpdateDivider=5

[meterBg]
Meter=Image
SolidColor=0,0,0,100
X=0
Y=0
W=150
H=45

[meterLevel]
Meter=String
MeasureName=measureBrightness
FontFace=Calibri
FontSize=16
FontColor=255,255,255
SolidColor=0,0,0,10
AntiAlias=1
X=0
Y=0
Text="%1"

[meterDown]
Meter=String
MeterStyle=meterLevel
Text="<<"
X=10R
Y=r
LeftMouseDownAction=[!CommandMeasure "measureBrightness" "lower"][!Update]

[meterUp]
Meter=String
MeterStyle=meterLevel
Text=">>"
X=R
Y=r
LeftMouseDownAction=[!CommandMeasure "measureBrightness" "raise"][!Update]

[meterBarBg]
Meter=Image
SolidColor=50,50,50,100
X=5
Y=30
W=140
H=10
LeftMouseUpAction=[!CommandMeasure "measureBrightness" "set $MouseX:%$"][!Update]
MouseScrollDownAction=[!CommandMeasure "measureBrightness" "lower"][!Update]
MouseScrollUpAction=[!CommandMeasure "measureBrightness" "raise"][!Update]

[meterBar]
Meter=Bar
MeasureName=measureBrightness
BarOrientation=Horizontal
X=r
Y=r
W=140
H=10
```