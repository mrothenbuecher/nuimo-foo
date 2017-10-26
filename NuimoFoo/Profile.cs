using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NuimoFoo
{

    class Profile
    {
        public String SwipeUp { get; set; }
        public String SwipeDown { get; set; }
        public String SwipeLeft { get; set; }
        public String SwipeRight { get; set; }
        public String RotateRight { get; set; }
        public String RotateLeft { get; set; }
        public String ButtonPress { get; set; }
        public String ButtonRelease { get; set; }
        public String FlyUp { get; set; }
        public String FlyDown { get; set; }
        public String FlyLeft { get; set; }
        public String FlyRight { get; set; }

        public Profile()
        {
            SwipeUp = "nuimokeytrigger:^j";
            SwipeDown = "nuimokeytrigger:^+j";
            RotateRight = "nuimokeytrigger:{DOWN}";
            RotateLeft = "nuimokeytrigger:{UP}";
            ButtonRelease = "nuimokeytrigger:{F11}{ENTER}";
            SwipeLeft = "nuimodde:?server=abas-EKS&topic=COMMAND&request=dis";
            SwipeRight = "profile.next";
        }

    }
}
