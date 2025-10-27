

## 1. Configurability. 
- [x] runtime configuration via secondary window
- [x] maximize number of frequency bins (to  create as much detail as possible) possible from the FFT function.
- [ ] resizeable bordless window
- [x] make sure the audio is displayed in usable, smart format. AKA, lineaer distribution of bars across frequency, etc. 
- [x] be able to configure the number of bars displayed across frequency (tune it down to say, 10 bins)
- [ ] display number of bins in settings 
- [x] adding in some sort of smart automatic gain control or compression into the magnitude across the frequency, so that the information shown isn't too small
- [ ] add segments up the bar rectangles, replicating LEDs or other dot-style representations, user adjustable
- [x] color gradients for the bars, vertically
## 2. Code organziation
- [x] Am I currenlty organizing this code "the right way"?  the answer could be yes, but i feel like code is often separated by function more than i'm currently set up.
- [ ] Can I abstract out the code in a way to make it easier to implement changes?
- [x] my folder structure is set up with BeAnal at the lowest directory structure, with the UI in a subfolder. that seems.. weird?
## 3. Advanced configs
- [x] strip the border
- [ ] transparency options inside the canvas
- [ ] run-time configurable always-on-top
- [ ] mouse hover-over makes the app briefly minimize (so it doesn't interfere with items below)
- [ ] integration (delay, soft roll off)
- [ ] Peak detection integration and variable roll-off

