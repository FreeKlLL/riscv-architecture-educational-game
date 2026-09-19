# From Gamification to Game-Based Learning

<img align="left" width="250" alt="RISCV_Logo" src="https://github.com/user-attachments/assets/ae0ea8a8-4043-43a4-a5e0-80a89aa7f2b0" />

### Design and Implementation of a Mobile Educational Game for the Multi-Cycle Processor

[![Unity Tests](https://github.com/GGalya1/riscv-architecture-educational-game/actions/workflows/unity-tests.yml/badge.svg)](https://github.com/GGalya1/riscv-architecture-educational-game/actions/workflows/unity-tests.yml)
[![Latest Tag](https://img.shields.io/github/v/tag/GGalya1/riscv-architecture-educational-game?label=Latest%20Build&color=orange)](https://github.com/GGalya1/riscv-architecture-educational-game/tags)
[![Unity Version](https://img.shields.io/badge/Unity-6-rtl.svg?logo=unity)](https://unity.com/)
![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20Windows%20%7C%20Linux-blueviolet)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

This repository contains the source code and Unity project for my Bachelor's Thesis at **TU Darmstadt**. 

The project is an educational mobile game designed to teach the fundamentals of computer organization, specifically focusing on the multi-cycle processor (Mehrtaktprozessor).

<br clear="all" />

## Table of Contents
- [Screenshots](#screenshots)
- [Play the Game](#play-the-game)
- [Project Overview](#project-overview)
- [Technical Specifications](#technical-specifications)
- [How to Open the Project](#how-to-open-the-project)
- [Building the Android APK Yourself](#building-the-android-apk-yourself)
- [Project Documentation](#project-documentation)
- [Roadmap](#roadmap)
- [Citation](#citation)

---

## Screenshots
 
| Processor Chapter | Extender Chapter | Charlie |
|:---:|:---:|:---:|
| ![Processor chapter screenshot](https://github.com/user-attachments/assets/fe24561c-b469-4691-9f0c-48a4a6403029) | ![Extender chapter screenshot](https://github.com/user-attachments/assets/e6497f01-985e-44f8-a6db-ccaf50ccaf32) | ![Charlie](https://github.com/user-attachments/assets/941eee70-3a21-4b7c-af36-3d44b8f96078) |

---

## Play the Game
If you want to play the game without opening the project in Unity, you can find the pre-built versions (APK for Android, EXE for Windows etc.) here:

<p>
 <a href="https://github.com/GGalya1/riscv-architecture-educational-game/tags"><img alt="Get it on GitHub" src="https://img.shields.io/github/v/tag/GGalya1/riscv-architecture-educational-game?label=Get%20it%20on%20GitHub&color=purple" height="60" align="middle"/></a>
 <a href="https://play.google.com/store/apps/details?id=com.edu.mehrtaktproz.sim&pcampaignid=web_share"><img alt="Get it on Google Play" src="https://play.google.com/intl/en_us/badges/static/images/badges/en_badge_web_generic.png" height="60" align="middle"/></a>
 <a href="https://ggalya.itch.io/riscv-adventure"><img alt="Available on itch.io" src="https://github.com/user-attachments/assets/63cfd5ae-5ea4-4ca3-9aec-97d7d0fa605a" height="40" align="middle"/></a>
</p>

---

## Project Overview

**RISC-V Adventure** is a quest through the layers of computer architecture. Players must master various components to eventually understand and build the "Great and Terrible" Multi-cycle Processor.

### Key Features:
* **31 Levels** across **8 Educational Chapters**:
    1.  **Register** - The basics of data storage.
    2.  **Multiplexer** - The art of selection.
    3.  **ALU** - The core of calculations.
    4.  **Memory** - Where instructions and data reside.
    5.  **Extender** - Preparing data for processing.
    6.  **Register File** - Managing multiple registers.
    7.  **Single-Cycle Processor** - Putting it all together in a single cycle. *(Work in Progress)*
    8.  **Multi-Cycle Processor** - The ultimate goal!
* **Companion System:** Meet **Charlie**, your digital assistant. She provides hints, explains theory, and allows players to choose between deep-dive explanations or jumping straight into the action.
* **Interactive Learning:** Concepts are taught through gameplay mechanics rather than just passive reading.

---

## Technical Specifications

* **Engine:** Unity 6 (Version: 6000.5.10f1)
* **Target Platforms:** Android (Primary), Windows (Evaluation Build), Linux (Evaluation Build)
* **Render Pipeline:** Universal Render Pipeline (URP)
* **Version Control:** Transitioned from Unity Version Control to Git.

---

## How to Open the Project

To explore the source code or build the project yourself:

1.  Install **Unity Hub**.
2.  Install **Unity 6 (6000.5.10f1)**.
3.  Clone this repository:  
    `https://github.com/GGalya1/riscv-architecture-educational-game.git`
4.  Add the project to Unity Hub and open it.
5.  *Note:* The initial import may take several minutes as Unity generates the `Library` folder.

---

## Building the Android APK Yourself

Android builds require a signing keystore, which is **not included** in this repository for security reasons (it is tied to the published Google Play identity). To build the project yourself, you have two options:

**Option A: Quick test build (recommended for just trying it out):**
1. Open `Edit -> Project Settings -> Player -> Publishing Settings`.
2. Make sure **Custom Keystore** is unchecked.
3. Build the project as usual, Unity will use a default debug keystore automatically.

**Option B: Build with your own signing key:**
1. Open `Edit -> Project Settings -> Player -> Publishing Settings`.
2. Check **Custom Keystore**, then open **Keystore Manager > Create New** and set your own keystore/alias passwords.
3. Build the project, the resulting APK will be signed with your own key.

*Note:* Builds you create yourself will not be signed with the same key as the official release, so they cannot be used to update an installation of the Play Store version - but they will install and run normally as a standalone APK.

---

## Project Documentation
You can find full information about used assets, project structure, code style, pull request guidelines, and more on our [GitHub Pages](https://ggalya1.github.io/riscv-architecture-educational-game/).

---

## Roadmap

**By Version 0.8.8 (End of August 2026)**
* [X] Finish **Chapter 7 - Single-Cycle Processor**
* [X] Additional polish and bugfixes based on user feedback from v0.8.2
* [X] English translation
* [X] Sound effects and music
* [X] Add tests to the repository
* [ ] Add custom hand-drawn sprites

**By Version 1.0.0 (End of November 2026)**
* [ ] Introduce Chapter 9 - Pipelined Processor
* [ ] Introduce Tutorial
* [ ] Introduce Story Mode and separate it from the level list (adaptive learning path based on player actions)
* [ ] Rework the in-game UI to better adapt it to mobile devices
* [ ] Add shaders to in-game processor objects

---

## Citation

If you use this project or want to cite the associated Bachelor's Thesis, please use the following metadata:

### BibTeX
```bibtex
@bachelorthesis{ggalya2026riscv,
  author       = {Galyautdinov Artur},
  title        = {From Gamification to Game-Based Learning: Design and Implementation of a Mobile Educational Game for the Multi-Cycle Processor},
  school       = {Technical University of Darmstadt},
  year         = {2026},
  type         = {Bachelor's Thesis},
  url          = {https://github.com/GGalya1/riscv-architecture-educational-game}
}
```
