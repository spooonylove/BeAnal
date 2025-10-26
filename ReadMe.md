

## 1. Configurability. 
- [x] runtime configuration via secondary window
- [ ] maximize number of frequency bins (to  create as much detail as possible) possible from the FFT function.
- [x] make sure the audio is displayed in usable, smart format. AKA, lineaer distribution of bars across frequency, etc. 
- [ ] be able to configure the number of bars displayed across frequency (tune it down to say, 10 bins)
- [x] adding in some sort of smart automatic gain control or compression into the magnitude across the frequency, so that the information shown isn't too small
- [ ] add segments up the bar rectangles, replicating LEDs or other dot-style representations
- [x] color gradients for the bars, vertically
## 2. Code organziation
- [x] Am I currenlty organizing this code "the right way"?  the answer could be yes, but i feel like code is often separated by function more than i'm currently set up.
- [ ] Can I abstract out the code in a way to make it easier to implement changes?
- [x] my folder structure is set up with BeAnal at the lowest directory structure, with the UI in a subfolder. that seems.. weird?
## 3. Advanced configs
- [x] strip the border
- [ ] transparency options inside the canvas
- [ ] resizeable bordless window
- [ ] run-time configurable always-on-top
- [ ] mouse hover-over makes the app briefly minimize (so it doesn't interfere with items below)
- [ ] integration (delay, soft roll off)
- [ ] Peak detection integration and variable roll-off
