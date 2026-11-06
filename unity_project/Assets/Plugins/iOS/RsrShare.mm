// Round 34: the iPhone share sheet for the result card (picture + post text). Called from Sharing.cs.
#import <UIKit/UIKit.h>

extern UIViewController *UnityGetGLViewController();

extern "C" void RsrShareImage(const char *path, const char *text)
{
    NSString *file = [NSString stringWithUTF8String:path];
    NSString *message = [NSString stringWithUTF8String:text];
    UIImage *image = [UIImage imageWithContentsOfFile:file];
    NSMutableArray *items = [NSMutableArray arrayWithObject:message];
    if (image != nil) [items addObject:image];
    UIActivityViewController *sheet = [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
    UIViewController *root = UnityGetGLViewController();
    // iPad shows the sheet as a popover: anchor it to the middle of the screen
    sheet.popoverPresentationController.sourceView = root.view;
    sheet.popoverPresentationController.sourceRect = CGRectMake(root.view.bounds.size.width / 2, root.view.bounds.size.height / 2, 1, 1);
    sheet.popoverPresentationController.permittedArrowDirections = 0;
    [root presentViewController:sheet animated:YES completion:nil];
}
