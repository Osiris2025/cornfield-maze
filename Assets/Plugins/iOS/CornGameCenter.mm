#import <Foundation/Foundation.h>
#import <GameKit/GameKit.h>

// Calls into Unity: the C# CornGameCenterReceiver on the "CornGameCenter" GameObject.
static void CornGC_SendMessage(const char *method, const char *msg)
{
    UnitySendMessage("CornGameCenter", method, msg);
}

// ---- authentication ----------------------------------------------------------------

extern "C" void _CornGC_Authenticate(void)
{
    GKLocalPlayer *player = [GKLocalPlayer localPlayer];
    if (player.authenticated)
    {
        CornGC_SendMessage("OnAuthSuccess", "");
        NSLog(@"CornMaze GC: already authenticated");
        return;
    }

    player.authenticateHandler = ^(UIViewController * __nullable viewController,
                                   NSError * __nullable error)
    {
        if (viewController != nil)
        {
            // The system wants to show a sign-in view controller, but the FSD says
            // we never block play.  The leaderboard button will trigger the system
            // prompt later when the player explicitly asks for it.
            CornGC_SendMessage("OnAuthError", "view-controller-required");
            NSLog(@"CornMaze GC: sign-in view controller required — deferring to leaderboard button");
            return;
        }
        if (player.authenticated)
        {
            CornGC_SendMessage("OnAuthSuccess", "");
            NSLog(@"CornMaze GC: authenticated as %@", player.alias);
        }
        else if (error != nil)
        {
            NSString *reason = [NSString stringWithFormat:@"%ld: %@",
                                (long)error.code, error.localizedDescription];
            CornGC_SendMessage("OnAuthError", [reason UTF8String]);
            NSLog(@"CornMaze GC: auth error — %@", reason);
        }
        else
        {
            CornGC_SendMessage("OnAuthError", "not-authenticated");
            NSLog(@"CornMaze GC: not authenticated (no error)");
        }
    };
}

// ---- score submission --------------------------------------------------------------

extern "C" void _CornGC_SubmitScore(int score, const char *leaderboardId)
{
    GKLocalPlayer *player = [GKLocalPlayer localPlayer];
    if (!player.authenticated)
    {
        NSLog(@"CornMaze GC: skip score submit — not authenticated");
        return;
    }

    NSString *lid = [NSString stringWithUTF8String:leaderboardId];

    [GKLeaderboard submitScore:score
                       context:0
                        player:player
                leaderboardIDs:@[lid]
             completionHandler:^(NSError * __nullable err)
    {
        if (err != nil)
            NSLog(@"CornMaze GC: score submit failed — %@", err.localizedDescription);
        else
            NSLog(@"CornMaze GC: submitted score %d to %@", score, lid);
    }];
}

// ---- leaderboard UI ----------------------------------------------------------------

@interface CornGCDelegate : NSObject <GKGameCenterControllerDelegate>
+ (instancetype)shared;
@end

@implementation CornGCDelegate
+ (instancetype)shared
{
    static CornGCDelegate *instance = nil;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ instance = [[CornGCDelegate alloc] init]; });
    return instance;
}
- (void)gameCenterViewControllerDidFinish:(GKGameCenterViewController *)gameCenterViewController
{
    [gameCenterViewController.presentingViewController
        dismissViewControllerAnimated:YES completion:nil];
}
@end

extern "C" void _CornGC_ShowLeaderboardUI(const char *leaderboardId)
{
    NSString *lid = [NSString stringWithUTF8String:leaderboardId];
    GKGameCenterViewController *vc = [[GKGameCenterViewController alloc] init];
    vc.gameCenterDelegate = [CornGCDelegate shared];
    vc.viewState = GKGameCenterViewControllerStateLeaderboards;
    vc.leaderboardIdentifier = lid;

    UIViewController *root = UIApplication.sharedApplication.keyWindow.rootViewController;
    if (root == nil)
    {
        NSLog(@"CornMaze GC: no root view controller — cannot show leaderboard");
        return;
    }
    [root presentViewController:vc animated:YES completion:nil];
    NSLog(@"CornMaze GC: showing leaderboard %@", lid);
}