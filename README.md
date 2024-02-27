## HaSuite / Harepacker resurrected
[![Github total downloads](https://img.shields.io/github/downloads/lastbattle/Harepacker-resurrected/total.svg)]() 
[![Github All Releases](https://img.shields.io/github/release/lastbattle/Harepacker-resurrected.svg)](https://github.com/lastbattle/Harepacker-resurrected/releases)
[![Github All Issues](https://img.shields.io/github/issues/lastbattle/Harepacker-resurrected.svg)](https://github.com/lastbattle/Harepacker-resurrected/issues)

A collection of tools for MapleStory, including a .wz file and level/field/map editor.

> Original thread: [HaSuite - HaCreator 2.1/HaRepacker 4.2.3](https://github.com/hadeutscher/HaSuite) | [Ragezone](http://forum.ragezone.com/f702/release-hasuite-hacreator-2-1-a-1068988/)

> Discussion thread: [Ragezone](https://forum.ragezone.com/f702/release-harepacker-resurrected-1149521/)

----

## Project contents
* HaCreator - MapleStory level editor
* HaRepacker - MapleStory .wz file editor
* HaSharedLibrary - A shared library between HaRepacker & HaCreator for mostly GUI
* squish-1.11\apng - (Unused for now, might consider .NET Core implementation of SIMD for images in future releases) [info](https://sjbrown.co.uk/?code=squish)
* spine-csharp 2.1.25 - 2D animation library [official website](https://github.com/EsotericSoftware/spine-runtimes) | [official website, spine demo](http://esotericsoftware.com/spine-demos) | [MapleStory dev's note](https://orangemushroom.net/2015/06/17/developers-note-maplestory-reboot-update-introduction-2-and-3/)
* UnitTest_WzFile - For testing of wz file across versions.

##### MapleLib2 by haha01haha01;
 - is based on MapleLib by Snow, WzLib by JonyLeeson, and information from Fiel\Koolk

----
## BUILD
### To build HaSuite, you need 
 - at least [Microsoft Visual Studio 2019](https://visualstudio.microsoft.com/vs/)
 - [Git](https://git-scm.com/downloads) or [Github, bundled](https://desktop.github.com/) for cloning, and downloading of related sub-module components in the repository.

### To run HaSuite, you need 
 - Microsoft .NET Framework 4.8 (usually already pre-installed in Windows 10, just do a Windows update)
  [otherwise, install here](https://dotnet.microsoft.com/download/visual-studio-sdks?utm_source=getdotnetsdk)  

### Modules / [Submodules](https://www.atlassian.com/git/tutorials/git-submodule) used
- [Spine-Runtime](https://github.com/EsotericSoftware/spine-runtimes)
- [MapleLib](https://github.com/lastbattle/MapleLib) 

### Cloning
``` 
git clone https://github.com/lastbattle/Harepacker-resurrected.git
git submodule init
git submodule update
``` 

----

## Development

Please note that this is a community-driven project that I work on in my free time. Don't expect any issues to be fixed or new features to be added quickly.

Want to support the development?

**BTC**: [3AEEJKaTNuw8KoafKNevpMsP2tVmaip4Fx](https://blockstream.info/address/3AEEJKaTNuw8KoafKNevpMsP2tVmaip4Fx)

Would you like to contribute to this project instead? If so, you can fork the project and create a pull request. It's all free!


----

![Harepacker](https://user-images.githubusercontent.com/4586194/109911770-a7d45e80-7ce5-11eb-9843-e4414bb6016f.png)

![Image preview](https://user-images.githubusercontent.com/4586194/109911721-85dadc00-7ce5-11eb-9111-4e2bfdbf5551.png)

![Spine image preview](https://user-images.githubusercontent.com/4586194/109911553-43b19a80-7ce5-11eb-8495-206a9c79d76f.png)

![Limen: Where the world ends](https://user-images.githubusercontent.com/4586194/208673934-e4300f74-8b6f-4866-a778-f7e675355ced.png)

![Rainbow Street Amherst](https://user-images.githubusercontent.com/4586194/208673762-4207a6c5-0f04-42a1-8f32-f6cd39598409.jpg)

![Oblivion Lake](https://user-images.githubusercontent.com/4586194/208673402-8c28c9f4-72da-4c8b-a818-43ed053cf126.png)


----
## License

MIT
```
Copyright (c) 2018~2023, LastBattle https://github.com/lastbattle
Copyright (c) 2010~2013, haha01haha http://forum.ragezone.com/f701/release-universal-harepacker-version-892005/

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

```
