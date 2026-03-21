#Requires AutoHotkey v2.0
#Warn

deviceName := "Joel-AP4"
DllCall("LoadLibrary", "str", "Bthprops.cpl", "ptr")

BLUETOOTH_DEVICE_SEARCH_PARAMS := Buffer(24 + A_PtrSize * 2, 0)
NumPut("uint", 24 + A_PtrSize * 2, BLUETOOTH_DEVICE_SEARCH_PARAMS, 0)
NumPut("uint", 1, BLUETOOTH_DEVICE_SEARCH_PARAMS, 4)   ; fReturnAuthenticated

BLUETOOTH_DEVICE_INFO := Buffer(560, 0)
NumPut("uint", 560, BLUETOOTH_DEVICE_INFO, 0)

foundDevice := 0

loop
{
    if (A_Index = 1)
    {
        foundDevice := DllCall("Bthprops.cpl\BluetoothFindFirstDevice", "ptr", BLUETOOTH_DEVICE_SEARCH_PARAMS, "ptr", BLUETOOTH_DEVICE_INFO, "ptr")
        if !foundDevice
        {
            MsgBox "No bluetooth devices found"
            return
        }
    }
    else
    {
        if !DllCall("Bthprops.cpl\BluetoothFindNextDevice", "ptr", foundDevice, "ptr", BLUETOOTH_DEVICE_INFO)
        {
            MsgBox "Device not found"
            break
        }
    }

    if (StrGet(BLUETOOTH_DEVICE_INFO.Ptr + 64, "UTF-16") = deviceName)
    {
        Handsfree := Buffer(16, 0)
        DllCall("ole32\CLSIDFromString", "wstr", "{0000111e-0000-1000-8000-00805f9b34fb}", "ptr", Handsfree)
        AudioSink := Buffer(16, 0)
        DllCall("ole32\CLSIDFromString", "wstr", "{0000110b-0000-1000-8000-00805f9b34fb}", "ptr", AudioSink)

        hr1 := DllCall("Bthprops.cpl\BluetoothSetServiceState", "ptr", 0, "ptr", BLUETOOTH_DEVICE_INFO, "ptr", Handsfree, "uint", 0)   ; disable voice
        hr2 := DllCall("Bthprops.cpl\BluetoothSetServiceState", "ptr", 0, "ptr", BLUETOOTH_DEVICE_INFO, "ptr", AudioSink, "uint", 0)   ; disable music

        if (hr1 = 0) && (hr2 = 0)
        {
            break
        }
        else
        {
            hr1 := DllCall("Bthprops.cpl\BluetoothSetServiceState", "ptr", 0, "ptr", BLUETOOTH_DEVICE_INFO, "ptr", Handsfree, "uint", 1)   ; enable voice
            hr2 := DllCall("Bthprops.cpl\BluetoothSetServiceState", "ptr", 0, "ptr", BLUETOOTH_DEVICE_INFO, "ptr", AudioSink, "uint", 1)   ; enable music
            break
        }
    }
}

DllCall("Bthprops.cpl\BluetoothFindDeviceClose", "ptr", foundDevice)
; MsgBox "Done"
ExitApp