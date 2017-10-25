# NuimoFoo

Universal Windows app for Nuimo which triggers apps. It's based on [nuimo-windows-demo](https://github.com/getsenic/nuimo-windows-demo).

## functionality
You can assign to each nuimo event e.g. ButtonPress, SwipeLeft, ... an  [application call](https://docs.microsoft.com/de-de/windows/uwp/launch-resume/launch-default-app)

These settings are defined in profiles. Each profile is a JSON file which looks something like this:
```json
{
  "SwipeUp": "nuimokeytrigger:^j",
  "SwipeDown": "nuimokeytrigger:^+j",
  "SwipeLeft": "nuimodde:?server=abas-EKS&topic=COMMAND&request=dis",
  "SwipeRight": null,
  "RotateRight": "nuimokeytrigger:{DOWN}",
  "RotateLeft": "nuimokeytrigger:{UP}",
  "ButtonPress": "nuimokeytrigger:{F11}{ENTER}",
  "ButtonRelease": null,
  "FlyUp": null,
  "FlyDown": null,
  "FlyLeft": null,
  "FlyRight": null
}
```

You can change between profiles on the run.

I've created two example apps
- [nuimokeytrigger, to trigger key types](https://github.com/mrothenbuecher/NuimoKeytrigger)
- [nuimodde,to make dde calls](https://github.com/mrothenbuecher/nuimodde)

### attention
Rotate events are only triggerd if the absolute value of the event is bigger 20.

If the FlyEvent value is bigger or equal 135 it will cause the FlyUp trigger, if it's less or equal 115 and bigger 1 it will trigger FlyDown. 

## requirements
 Windows 10
