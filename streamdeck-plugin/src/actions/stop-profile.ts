import {
  action,
  KeyDownEvent,
  SingletonAction,
  WillAppearEvent,
} from "@elgato/streamdeck";

import { stopProfile, getStatus } from "../xoutput-cli";

/**
 * Action to stop the running XOutputRenew profile.
 */
@action({ UUID: "com.xoutputrenew.stop-profile" })
export class StopProfileAction extends SingletonAction {
  /**
   * Called when the action appears on the Stream Deck.
   */
  override async onWillAppear(ev: WillAppearEvent): Promise<void> {
    await ev.action.setTitle("Stop");
  }

  /**
   * Called when the key is pressed.
   */
  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    try {
      // Check if anything is running first
      const status = await getStatus();

      if (!status.isRunning) {
        // Nothing to stop
        await ev.action.showOk();
        return;
      }

      const success = await stopProfile();

      if (success) {
        // Show success feedback
        await ev.action.showOk();
      } else {
        // Show error feedback
        await ev.action.showAlert();
      }
    } catch (error) {
      console.error("Failed to stop profile:", error);
      await ev.action.showAlert();
    }
  }
}
