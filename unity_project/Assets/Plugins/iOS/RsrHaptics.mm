// Round 32: short haptic ticks on iPhone (UIKit feedback generators; Taptic Engine).
// Called from Haptics.cs through [DllImport("__Internal")]; Unity calls it on the main thread.
#import <UIKit/UIKit.h>

extern "C" void RsrHapticImpact(int style)
{
    if (style == 2)
    {
        UINotificationFeedbackGenerator *success = [[UINotificationFeedbackGenerator alloc] init];
        [success notificationOccurred:UINotificationFeedbackTypeSuccess];
        return;
    }
    UIImpactFeedbackGenerator *impact = [[UIImpactFeedbackGenerator alloc] initWithStyle:(style == 0 ? UIImpactFeedbackStyleLight : UIImpactFeedbackStyleMedium)];
    [impact impactOccurred];
}
