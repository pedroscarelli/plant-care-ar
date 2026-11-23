// Camera permission straight from AVFoundation.
// Unity's own Application.RequestUserAuthorization is compiled out on iOS when the project doesn't use
// WebCamTexture (UNITY_USES_WEBCAM 0): it then reports "denied" without ever showing the system prompt.
#import <AVFoundation/AVFoundation.h>

extern "C"
{
    // 0 = not asked yet, 1 = restricted, 2 = denied, 3 = authorized (AVAuthorizationStatus).
    int CuidAR_GetCameraStatus()
    {
        return (int)[AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo];
    }

    // Shows the system prompt (only the first time; afterwards iOS answers with the saved choice).
    void CuidAR_RequestCamera()
    {
        [AVCaptureDevice requestAccessForMediaType:AVMediaTypeVideo completionHandler:^(BOOL granted) {}];
    }
}
