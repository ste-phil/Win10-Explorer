# Win10-Explorer
Welcome to the new Windows 10 File Explorer made to replace the outdated current one.
It uses the new Fluent Design from Microsoft to integrate seamingless into the new Windows 10 experience.

## Features:
 * Beautiful design
 * Search folders quickly or do a deep search (type "d searchInput" into the search bar) 
 * Fast and responsive folder enumeration
 * View Zip Archives directly via the Explorer

![File Explorer](https://i.gyazo.com/e3355c6227c5355dc1732ebebd8c6b20.jpg)
![File Explorer](https://i.gyazo.com/56c23c2661a181314e7ea7e320f2dd54.png)
![File Explorer](https://i.gyazo.com/8eb622cc59bb30de41fd5656650cdd91.gif)

## Usage:
  * Download or clone the repo
  * Enable Windows developer settings
  * Create developer certificate to be able to deploy it (Type the following into powershell)
  ```powershell
New-SelfSignedCertificate -Type Custom -Subject "CN=Philipp" -KeyUsage DigitalSignature -FriendlyName "Philipp" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
  ```
  * Build it (should work for Windows 10 Version 1809 and above)
  * Deploy it
  * Grant permission (Needs file system permission [Help link](https://support.microsoft.com/en-us/help/10557/windows-10-app-permissions))
  * Run it
 
## Feel free to help, to improve the new windows 10 file explorer :)
