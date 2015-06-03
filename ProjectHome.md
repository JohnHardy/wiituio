Ever wanted to use a Wiimote to get multi-touch on your Windows 7 PC?

WiiTUIO might just be what you're looking for.  If you would like some instructions there is a good guide here (along with a few handy scripts to automate things) http://goo.gl/Vp9dt.

Interested in doing the same thing with a Kinect?   Check out Ubi Displays: http://code.google.com/p/ubidisplays/

This project aims to improve the stability of the IR sources captured by the Wiimote using some thresholds and spatio-temporal classification.  The application can generate native Windows 7 touch events as well as TUIO messages from the stabilised data.

Each raw IR source captured by the Wiimote is either assigned to the best existing tracked source or generates a new tracker.  This means that the touch events can be generated from stable data without the jitter (namely, false-positives generated between two IR sources and the unordered source buffer) that occurs when trying to use the Wiimote to capture true multi-touch IR.

<a href='http://www.youtube.com/watch?feature=player_embedded&v=rbMafItax6Y' target='_blank'><img src='http://img.youtube.com/vi/rbMafItax6Y/0.jpg' width='425' height=344 /></a>

This project was created as part of the CoffeeTable project.  Find out more here: http://highwire-dtc.com/coffeetable/