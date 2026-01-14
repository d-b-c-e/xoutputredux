import {
  action,
  KeyDownEvent,
  SingletonAction,
  WillAppearEvent,
  SendToPluginEvent,
} from "@elgato/streamdeck";

import { startProfile, listProfiles, getStatus } from "../xoutput-cli";

/**
 * Settings for the Start Profile action.
 */
interface StartProfileSettings {
  profileName?: string;
}

/**
 * Action to start an XOutputRenew profile.
 */
@action({ UUID: "com.xoutputrenew.start-profile" })
export class StartProfileAction extends SingletonAction<StartProfileSettings> {
  /**
   * Called when the action appears on the Stream Deck.
   */
  override async onWillAppear(ev: WillAppearEvent<StartProfileSettings>): Promise<void> {
    const settings = ev.payload.settings;

    // Set title to profile name if configured
    if (settings.profileName) {
      await ev.action.setTitle(settings.profileName);
    } else {
      await ev.action.setTitle("Start");
    }
  }

  /**
   * Called when the key is pressed.
   */
  override async onKeyDown(ev: KeyDownEvent<StartProfileSettings>): Promise<void> {
    const settings = ev.payload.settings;

    try {
      const success = await startProfile(settings.profileName);

      if (success) {
        // Show success feedback
        await ev.action.showOk();
      } else {
        // Show error feedback
        await ev.action.showAlert();
      }
    } catch (error) {
      console.error("Failed to start profile:", error);
      await ev.action.showAlert();
    }
  }

  /**
   * Called when the Property Inspector sends a message.
   */
  override async onSendToPlugin(ev: SendToPluginEvent<{ event: string }, StartProfileSettings>): Promise<void> {
    if (ev.payload.event === "getProfiles") {
      // Fetch profiles and send to Property Inspector
      const profiles = await listProfiles();
      await ev.action.sendToPropertyInspector({
        event: "profilesList",
        profiles: profiles,
      });
    }
  }
}
