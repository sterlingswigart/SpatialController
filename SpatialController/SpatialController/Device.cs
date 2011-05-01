using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;

using xn;

namespace TrackingNI
{
    class Device
    {
        private const int ACTIVATION_SEC = 1;
        private const int DEBOUNCE_SEC = 3;

        private bool inFocus;
        private DateTime lastActionTime;
        private DateTime focusStartTime;

        public Vector3D position;

        public Device(Vector3D position/*, TODO: device handler */)
        {
            this.inFocus = false;
            this.lastActionTime = DateTime.Now;
            this.focusStartTime = DateTime.Now;
            this.position = position;
        }

        public void isInFocus()
        {
            if (inFocus)
            {
                if (DateTime.Now - focusStartTime > new TimeSpan(0, 0, ACTIVATION_SEC)
                    && DateTime.Now - lastActionTime > new TimeSpan(0, 0, DEBOUNCE_SEC))
                {
                    // TODO: Turn device on or off.
                    lastActionTime = DateTime.Now;
                }
            }
            else
            {
                inFocus = true;
                focusStartTime = DateTime.Now;
            }
        }

        public void isNotInFocus()
        {
            if (inFocus)
            {
                // Reset state.
                inFocus = false;
            }
        }
    }
}
