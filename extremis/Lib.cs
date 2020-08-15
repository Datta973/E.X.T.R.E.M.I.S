using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;

namespace Osion
{
    public delegate void Method();
    public static class Lib
    {
        
        public static void setTimeout(Method meth,int t) {
            Thread th = new Thread(() => {
                Thread.Sleep(t);
                meth();
            });
            th.Start();
        }

        public static void animate(UIElement uIElement, DependencyProperty prop, int from, int to, int span, EventHandler callback = null)
        {
            System.Windows.Media.Animation.DoubleAnimation da = new System.Windows.Media.Animation.DoubleAnimation();
            da.From = from;
            da.To = to;
            da.Duration = new Duration(TimeSpan.FromMilliseconds(span));
            if (callback != null)
            {
                da.Completed += callback;
            }
            uIElement.BeginAnimation(prop, da);
        }
    }
}
