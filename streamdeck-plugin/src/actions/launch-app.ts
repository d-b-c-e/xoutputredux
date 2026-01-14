import {
  action,
  KeyDownEvent,
  SingletonAction,
  WillAppearEvent,
} from "@elgato/streamdeck";

import { launchApp } from "../xoutput-cli";

/**
 * Action to launch the XOutputRenew application.
 */
@action({ UUID: "com.xoutputrenew.launch-app" })
export class LaunchAppAction extends SingletonAction {
  override async onWillAppear(ev: WillAppearEvent): Promise<void> {
    await ev.action.setTitle("Launch\nApp");
  }

  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    try {
      const success = await launchApp();
      if (success) {
        await ev.action.showOk();
      } else {
        await ev.action.showAlert();
      }
    } catch (error) {
      console.error("Failed to launch app:", error);
      await ev.action.showAlert();
    }
  }
}
