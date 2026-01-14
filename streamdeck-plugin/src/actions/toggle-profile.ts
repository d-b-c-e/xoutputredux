import {
  action,
  KeyDownEvent,
  SingletonAction,
  WillAppearEvent,
  SendToPluginEvent,
  DidReceiveSettingsEvent,
} from "@elgato/streamdeck";

import { toggleProfile, getStatus, listProfiles } from "../xoutput-cli";

/**
 * Settings for the Toggle Profile action.
 */
interface ToggleProfileSettings {
  profileName?: string;
}

/**
 * Action to toggle an XOutputRenew profile on/off.
 */
@action({ UUID: "com.xoutputrenew.toggle-profile" })
export class ToggleProfileAction extends SingletonAction<ToggleProfileSettings> {
  private statusCheckInterval: NodeJS.Timeout | null = null;
  private actionContexts: Map<string, { profileName?: string }> = new Map();

  /**
   * Called when the action appears on the Stream Deck.
   */
  override async onWillAppear(ev: WillAppearEvent<ToggleProfileSettings>): Promise<void> {
    const settings = ev.payload.settings;
    const context = ev.action.id;

    // Store the action context for status updates
    this.actionContexts.set(context, { profileName: settings.profileName });

    // Set title
    if (settings.profileName) {
      await ev.action.setTitle(settings.profileName);
    } else {
      await ev.action.setTitle("Toggle");
    }

    // Update the state based on current status
    await this.updateState(ev.action, settings.profileName);

    // Start periodic status check if not already running
    if (!this.statusCheckInterval) {
      this.startStatusCheck();
    }
  }

  /**
   * Called when the action is removed from the Stream Deck.
   */
  override async onWillDisappear(ev: WillAppearEvent<ToggleProfileSettings>): Promise<void> {
    this.actionContexts.delete(ev.action.id);

    // Stop status check if no more actions
    if (this.actionContexts.size === 0 && this.statusCheckInterval) {
      clearInterval(this.statusCheckInterval);
      this.statusCheckInterval = null;
    }
  }

  /**
   * Called when settings are received.
   */
  override async onDidReceiveSettings(ev: DidReceiveSettingsEvent<ToggleProfileSettings>): Promise<void> {
    const settings = ev.payload.settings;
    this.actionContexts.set(ev.action.id, { profileName: settings.profileName });

    // Update title
    if (settings.profileName) {
      await ev.action.setTitle(settings.profileName);
    } else {
      await ev.action.setTitle("Toggle");
    }

    // Update state
    await this.updateState(ev.action, settings.profileName);
  }

  /**
   * Called when the key is pressed.
   */
  override async onKeyDown(ev: KeyDownEvent<ToggleProfileSettings>): Promise<void> {
    const settings = ev.payload.settings;

    if (!settings.profileName) {
      // No profile configured
      await ev.action.showAlert();
      return;
    }

    try {
      const isRunning = await toggleProfile(settings.profileName);

      // Update the state
      await ev.action.setState(isRunning ? 1 : 0);
      await ev.action.showOk();
    } catch (error) {
      console.error("Failed to toggle profile:", error);
      await ev.action.showAlert();
    }
  }

  /**
   * Called when the Property Inspector sends a message.
   */
  override async onSendToPlugin(ev: SendToPluginEvent<{ event: string }, ToggleProfileSettings>): Promise<void> {
    if (ev.payload.event === "getProfiles") {
      // Fetch profiles and send to Property Inspector
      const profiles = await listProfiles();
      await ev.action.sendToPropertyInspector({
        event: "profilesList",
        profiles: profiles,
      });
    }
  }

  /**
   * Updates the button state based on current XOutputRenew status.
   */
  private async updateState(action: any, profileName?: string): Promise<void> {
    if (!profileName) {
      await action.setState(0);
      return;
    }

    try {
      const status = await getStatus();
      const isThisProfile = status.isRunning && status.profileName === profileName;
      await action.setState(isThisProfile ? 1 : 0);
    } catch (error) {
      // On error, assume off
      await action.setState(0);
    }
  }

  /**
   * Starts periodic status checking to keep button states in sync.
   */
  private startStatusCheck(): void {
    this.statusCheckInterval = setInterval(async () => {
      try {
        const status = await getStatus();

        // Update all toggle actions
        for (const [contextId, settings] of this.actionContexts) {
          // Get action by ID - this is a simplified approach
          // In a real implementation, you'd track action references
        }
      } catch (error) {
        // Ignore status check errors
      }
    }, 5000); // Check every 5 seconds
  }
}
