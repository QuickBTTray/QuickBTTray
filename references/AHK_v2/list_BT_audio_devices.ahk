#Requires AutoHotkey v2.0
#Warn

DllCall("LoadLibrary", "str", "Bthprops.cpl", "ptr")

; Search params: return authenticated + remembered devices
BLUETOOTH_DEVICE_SEARCH_PARAMS := Buffer(24 + A_PtrSize * 2, 0)
NumPut("uint", 24 + A_PtrSize * 2, BLUETOOTH_DEVICE_SEARCH_PARAMS, 0)
NumPut("uint", 1, BLUETOOTH_DEVICE_SEARCH_PARAMS, 4)   ; fReturnAuthenticated
NumPut("uint", 1, BLUETOOTH_DEVICE_SEARCH_PARAMS, 8)   ; fReturnRemembered

; BLUETOOTH_DEVICE_INFO layout (64-bit):
;   0:  dwSize (4)
;   4:  padding (4)
;   8:  Address (8) — 6 MAC bytes, little-endian
;  16:  ulClassofDevice (4)
;  20:  fConnected (4)
;  24:  fRemembered (4)
;  28:  fAuthenticated (4)
;  32:  stLastSeen (16)
;  48:  stLastUsed (16)
;  64:  szName[248] (496)
; Total: 560
BLUETOOTH_DEVICE_INFO := Buffer(560, 0)
NumPut("uint", 560, BLUETOOTH_DEVICE_INFO, 0)

deviceList := []

foundHandle := DllCall("Bthprops.cpl\BluetoothFindFirstDevice",
    "ptr", BLUETOOTH_DEVICE_SEARCH_PARAMS,
    "ptr", BLUETOOTH_DEVICE_INFO,
    "ptr")

if !foundHandle {
    MsgBox "No Bluetooth devices found."
    ExitApp
}

loop {
    name := StrGet(BLUETOOTH_DEVICE_INFO.Ptr + 64, "UTF-16")

    ; Build MAC address string from 6 bytes at offset 8 (big-endian display)
    addrStr := ""
    loop 6 {
        byte := NumGet(BLUETOOTH_DEVICE_INFO, 8 + (6 - A_Index), "uchar")
        addrStr .= (A_Index > 1 ? ":" : "") . Format("{:02X}", byte)
    }

    connected     := NumGet(BLUETOOTH_DEVICE_INFO, 20, "uint") ? "Yes" : "No"
    remembered    := NumGet(BLUETOOTH_DEVICE_INFO, 24, "uint") ? "Yes" : "No"
    authenticated := NumGet(BLUETOOTH_DEVICE_INFO, 28, "uint") ? "Yes" : "No"

    deviceList.Push({ name: name, address: addrStr, connected: connected,
        remembered: remembered, authenticated: authenticated })

    if !DllCall("Bthprops.cpl\BluetoothFindNextDevice", "ptr", foundHandle, "ptr", BLUETOOTH_DEVICE_INFO)
        break
}

DllCall("Bthprops.cpl\BluetoothFindDeviceClose", "ptr", foundHandle)

; --- GUI ---
myGui := Gui("+Resize", "Bluetooth Devices (" deviceList.Length " found)")
myGui.SetFont("s10", "Segoe UI")

lv := myGui.Add("ListView", "r15 w680 +LV0x10000",
    ["Name", "Address", "Connected", "Remembered", "Authenticated"])

for device in deviceList
    lv.Add("", device.name, device.address, device.connected, device.remembered, device.authenticated)

lv.ModifyCol(1, 200)   ; Name
lv.ModifyCol(2, 140)   ; Address
lv.ModifyCol(3, 80)    ; Connected
lv.ModifyCol(4, 95)    ; Remembered
lv.ModifyCol(5, 115)   ; Authenticated

btnCopySel := myGui.Add("Button", "w140 y+6", "Copy Selected (Ctrl+C)")
btnCopyAll := myGui.Add("Button", "x+8 w120", "Copy All")

btnCopySel.OnEvent("Click", CopySelected)
btnCopyAll.OnEvent("Click", CopyAll)

; Right-click context menu on ListView
ctxMenu := Menu()
ctxMenu.Add("Copy Row", CopySelected)
ctxMenu.Add("Copy All", CopyAll)
lv.OnEvent("ContextMenu", (*) => ctxMenu.Show())

; Ctrl+C while GUI is active
myGui.OnEvent("Close", (*) => ExitApp())
myGui.Show()

; Hotkey only active while our GUI window is in focus
#HotIf WinActive("ahk_id " myGui.Hwnd)
^c:: CopySelected()
#HotIf

CopySelected(*) {
    row := lv.GetNext(0, "Focused")
    if !row {
        row := lv.GetNext(0)   ; fall back to first selected
    }
    if !row {
        ToolTip "No row selected."
        SetTimer(() => ToolTip(), -1500)
        return
    }
    cols := ["Name", "Address", "Connected", "Remembered", "Authenticated"]
    text := ""
    for i, col in cols
        text .= col ": " lv.GetText(row, i) "`n"
    A_Clipboard := RTrim(text, "`n")
    ToolTip "Copied to clipboard!"
    SetTimer(() => ToolTip(), -1500)
}

CopyAll(*) {
    header := Format("{:-30}{:-20}{:-12}{:-13}{}`n", "Name", "Address", "Connected", "Remembered", "Authenticated")
    sep    := Format("{:-30}{:-20}{:-12}{:-13}{}`n", "----", "-------", "---------", "----------", "-------------")
    text   := header . sep
    loop lv.GetCount() {
        row := A_Index
        text .= Format("{:-30}{:-20}{:-12}{:-13}{}`n",
            lv.GetText(row, 1),
            lv.GetText(row, 2),
            lv.GetText(row, 3),
            lv.GetText(row, 4),
            lv.GetText(row, 5))
    }
    A_Clipboard := text
    ToolTip "All rows copied to clipboard!"
    SetTimer(() => ToolTip(), -1500)
}
