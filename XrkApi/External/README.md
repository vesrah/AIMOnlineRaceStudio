# External dependencies

Native Windows (x64) DLLs required by **MatLabXRK-2022-64-ReleaseU.dll** at runtime. They are copied to the build/publish output so the XrkApi service can load the XRK reader.

| File                | Purpose              |
|---------------------|----------------------|
| MatLabXRK-*.dll     | XRK file reading     |
| libiconv-2.dll      | Character encoding   |
| libxml2-2.dll       | XML parsing          |
| libz.dll            | Compression          |
| pthreadVC2_x64.dll  | Threading (Windows)  |
