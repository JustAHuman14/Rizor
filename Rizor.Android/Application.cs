using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace Rizor.Android
{
    [Application]
    public class Application : AvaloniaAndroidApplication<Core.App>
    {
        protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder)
            .WithInterFont();
        }
    }
}
