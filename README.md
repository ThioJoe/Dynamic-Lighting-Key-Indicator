<h1 align = 'center'>
    <img 
        src = 'https://github.com/user-attachments/assets/0ca66aff-a5a8-4ffb-9c54-949797e1d711' 
        width = "100 
        alt = "App icon of a rainbow light bulb" 
    >
    <br>
    Dynamic Lighting Key Indicator
    <br>
</h1>

An app that uses the Windows 11 "Dynamic Lighting" API to change the color of the **`Num Lock`**, **`Caps Lock`**, and **`Scroll Lock`** keyboard keys based on their toggle status.

-------

## Features
- Set the On/Off color and brightness for each toggle key
- Set the 'default' color that applies to the rest of the keyboard
- Option to sync any toggle key's On/Off color to be the same as the default
- Control via local Windows URL protocol ( `key-lighting-indicator://` ) and parameters
- Auto-reconnects to saved device when program starts up

## Requirements:
- Windows 11 (23H2)
- A compatible keyboard (Support for Windows 11 Dynamic Lighting, with per-key RGB)

# Screenshot

<p align="center">
<img width="591" alt="image" src="https://github.com/user-attachments/assets/0260ea7c-9a71-47ca-b2a8-2238e8884870" />
</p>

-------

# URL Protocol Control
You can control the application settings using the `key-lighting-indicator://` protocol. The general format is:
```url
key-lighting-indicator://set?parameter1=value1&parameter2=value2
```

#### Example Using PowerShell - *(Note: the double quotes are important)*
```powershell
Start-Process "key-lighting-indicator://set?global_brightness=100"
```


## Protocol Parameters
- `global_brightness`: Integer (0-100) - Sets brightness for all keys
- `standardkeycolor`: RGB values (e.g., "255,0,0") or "default" - Sets the color for non-toggle keys
- `numlockcolor_on`: RGB values or "default" - Sets Num Lock's active color
- `numlockcolor_off`: RGB values or "default" - Sets Num Lock's inactive color
- `capslockcolor_on`: RGB values or "default" - Sets Caps Lock's active color
- `capslockcolor_off`: RGB values or "default" - Sets Caps Lock's inactive color
- `scrolllockcolor_on`: RGB values or "default" - Sets Scroll Lock's active color
- `scrolllockcolor_off`: RGB values or "default" - Sets Scroll Lock's inactive color



## Examples

- Set global brightness to 50%:
     ```url
     key-lighting-indicator://set?global_brightness=50
     ```

- Set all non-toggle keys to red:
    ```url
    key-lighting-indicator://set?standardkeycolor=255,0,0
    ```

- Set Caps Lock colors (green when on, red when off):
    ```url
    key-lighting-indicator://set?capslockcolor_on=0,255,0&capslockcolor_off=255,0,0
    ```

- Set multiple parameters at once:
    ```url
    key-lighting-indicator://set?global_brightness=75&standardkeycolor=0,0,255&numlockcolor_on=50,50,0
    ```

- Reset Scroll Lock colors to match the standard key color:
    ```url
    key-lighting-indicator://set?scrolllockcolor_on=default&scrolllockcolor_off=default
    ```
