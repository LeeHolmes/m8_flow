# m8_flow
_... The only sense you need is your ears._

With the [Dirtywave M8](https://dirtywave.com/), one thing that's unique is how many people say, "Muscle memory takes over". Because of that, the Dirtywave M8 might be the first electronic music device that could truly support an audio-only "flow mode", where you could compose a song entirely in the dark and have sound be your only source of input.
 
This would also make it 100% accessible to electronic musicians that are blind, which is clearly something that makes the world a better place.

Flow mode is an experimental implementation of this audio-only interface. You can hear a demo here: https://youtu.be/Kd5CdkrCu3s.

## Installing

m8_flow requires a working, plugged in, [Dirtywave M8 Headless](https://github.com/DirtyWave/M8HeadlessFirmware). It also heavily leverages the [m8c project](https://github.com/laamaa/m8c), which is included directly in this project to make distribution easier.

To install, download [m8_flow.zip](https://github.com/LeeHolmes/m8_flow/releases/download/v0.1-alpha/m8_flow.zip) and unzip.

## Running

Run 'm8_flow.exe'. You must have your m8 headless attached and working.

You will see the m8_flow console window launch, followed by the m8c screen displaying the Dirtywave m8 user interface. Then, the m8_flow window will show the content that it's detecting from the m8c window and begin announcing what's happening on screen.

Switch to the m8c window and begin playing with it like usual. You will hear m8_flow announcing your changes as you make them.

Close your eyes. You should be able to write an entire song using your ears alone.

## Known issues

- This project is based on screen scraping of the m8c client window. While running m8_flow, make sure that the m8c window that launches is visible and on the same monitor as m8_flow.
- This project is incomplete. There is full support for the following pages: Song, Chain, Phrase, Instrument, Table, and Project. Lesser used pages have not yet been implemented (but this is an open source project :))

## Current Status

The proof-of-concept has shown to be extremely capable and was useful for quickly fleshing out design ideas. With the goals of being cross-platform and possibly being integrated into the Dirtywave M8 itself some day, I have pivoted the architecture and am working on a redesign, with the work in progress here: [m8c_flow](https://github.com/LeeHolmes/m8c_flow):

- It is now implemented in C so that it can be used cross-platform and embedded in a Teensy (M8's hardware platform)
- It is now being implemented as an update to the [m8c client](https://github.com/laamaa/m8c) itself
- It now parses / understands M8's Headless protocol rather than screen scraping
- It has moved off of the Windows Speech Synthesis SDK and instead to the open-source FLITE speech synthesis library
- It has trimmed FLITE down to its [bare minimum](https://github.com/LeeHolmes/flite_minimal)
- *work in progress* It has updated FLITE from depending on Linux-specific ALSA sound library to the cross-platform PortAudio library
