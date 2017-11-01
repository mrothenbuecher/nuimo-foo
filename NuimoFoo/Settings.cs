using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NuimoFoo
{

    class Settings
    {
        public bool automaticSwitchBetweenProfiles { get; set; }
        public int rotateThreshold { get; set; }

        public Settings()
        {
            automaticSwitchBetweenProfiles = false;
            rotateThreshold = 10;
    }

    }
}
