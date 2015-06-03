#WiiTUIO
Ever wanted to use a Wiimote to get multi-touch on your Windows 7 PC?
WiiTUIO might just be what you're looking for. If you would like some instructions there is a good guide here (along with a few handy scripts to automate things) http://goo.gl/Vp9dt.

WiiTUIO improves the stability of the IR sources captured by the Wiimote using some thresholds and spatio-temporal classification. The application can generate native Windows 7 touch events as well as TUIO messages from the stabilised data.

Interested in doing the same thing with a Kinect? Check out Ubi Displays: http://code.google.com/p/ubidisplays/

<a href="http://www.youtube.com/watch?feature=player_embedded&v=rbMafItax6Y
" target="_blank"><img src="http://img.youtube.com/vi/rbMafItax6Y/0.jpg" 
alt="Watch a preview" width="640" height="480" border="10" /></a>

Each raw IR source captured by the Wiimote is either assigned to the best existing tracked source or generates a new tracker. This means that the touch events can be generated from stable data without the jitter (namely, false-positives generated between two IR sources and the unordered source buffer) that occurs when trying to use the Wiimote to capture true multi-touch IR.

**Created as part of the CoffeeTable project. Find out more here: http://highwire-dtc.com/coffeetable/**

AUTHORS
================

WiiTUIO v2
John Hardy & Christopher Bull
hardyj2{at}unix.lancs.ac.uk & c.bull{at}lancaster.ac.uk
HighWire Programme, Lancaster University
22-06-2010

REQUIREMENTS
================
(1) Windows 7 is required in order to use generate touch events.
(2) The UniSoftHID driver used with MultiTouchVista.  The code to support touch events is based on (Multitouch.Driver.Logic) by Nesher. As such, I reccomend that you use the whole 'MultiTouchVista' package for better support.
    The UniSoftHID driver can be found bundled with 'MultiTouchVista' here:  http://multitouchvista.codeplex.com/releases/view/28979

NOTES
================
Still reading?  There are a couple of things you should know before using this software:
(1) You need to click Connect first.
(2) Ensure you click calibrate at least once after you have created a connection.

ACKNOWLEDGEMENTS
================

Johnny Chung Lee: 	http://johnnylee.net/projects/wii/
Brian Peek: 		http://www.brianpeek.com/
Nesher:			http://www.codeplex.com/site/users/view/nesher
TUIO Project: 		http://www.tuio.org
MultiTouchVista:	http://multitouchvista.codeplex.com/
OSC.NET Library:	http://luvtechno.net/
WiimoteLib 1.7:		http://wiimotelib.codeplex.com/
HIDLibrary:		http://hidlibrary.codeplex.com/
WPFNotifyIcon:		http://www.hardcodet.net/projects/wpf-notifyicon

Please respect each invdivudal project's software license.
