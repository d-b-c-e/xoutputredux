import {
  action,
  DialDownEvent,
  DialRotateEvent,
  SingletonAction,
  WillAppearEvent,
  TouchTapEvent,
} from "@elgato/streamdeck";

import { listProfiles, toggleProfile, getStatus, XOutputProfile } from "../xoutput-cli";

interface ProfileDialSettings {
  selectedIndex?: number;
}

/**
 * Encoder action to browse and toggle profiles.
 * - Rotate: Browse through available profiles
 * - Press: Toggle the selected profile on/off
 * - Touch: Show current profile name
 */
@action({ UUID: "com.xoutputrenew.profile-dial" })
export class ProfileDialAction extends SingletonAction<ProfileDialSettings> {
  private profiles: XOutputProfile[] = [];
  private selectedIndex: number = 0;
  private statusCheckInterval: NodeJS.Timeout | null = null;

  override async onWillAppear(ev: WillAppearEvent<ProfileDialSettings>): Promise<void> {
    // Load profiles
    await this.refreshProfiles();

    // Restore selected index from settings
    const settings = ev.payload.settings;
    if (settings.selectedIndex !== undefined && settings.selectedIndex < this.profiles.length) {
      this.selectedIndex = settings.selectedIndex;
    }

    await this.updateDisplay(ev.action);

    // Start periodic status check
    if (!this.statusCheckInterval) {
      this.statusCheckInterval = setInterval(async () => {
        await this.updateDisplay(ev.action);
      }, 3000);
    }
  }

  override async onWillDisappear(): Promise<void> {
    if (this.statusCheckInterval) {
      clearInterval(this.statusCheckInterval);
      this.statusCheckInterval = null;
    }
  }

  /**
   * Called when the dial is rotated.
   */
  override async onDialRotate(ev: DialRotateEvent<ProfileDialSettings>): Promise<void> {
    if (this.profiles.length === 0) {
      await this.refreshProfiles();
      if (this.profiles.length === 0) {
        return;
      }
    }

    // Update selected index based on rotation
    const ticks = ev.payload.ticks;
    this.selectedIndex += ticks;

    // Wrap around
    if (this.selectedIndex < 0) {
      this.selectedIndex = this.profiles.length - 1;
    } else if (this.selectedIndex >= this.profiles.length) {
      this.selectedIndex = 0;
    }

    // Save settings
    await ev.action.setSettings({ selectedIndex: this.selectedIndex });

    // Update display
    await this.updateDisplay(ev.action);
  }

  /**
   * Called when the dial is pressed.
   */
  override async onDialDown(ev: DialDownEvent<ProfileDialSettings>): Promise<void> {
    if (this.profiles.length === 0 || this.selectedIndex >= this.profiles.length) {
      await ev.action.showAlert();
      return;
    }

    const profile = this.profiles[this.selectedIndex];

    try {
      await toggleProfile(profile.name);
      await ev.action.showOk();
      await this.updateDisplay(ev.action);
    } catch (error) {
      console.error("Failed to toggle profile:", error);
      await ev.action.showAlert();
    }
  }

  /**
   * Called when the touchscreen is tapped (Stream Deck+).
   */
  override async onTouchTap(ev: TouchTapEvent<ProfileDialSettings>): Promise<void> {
    // Refresh profiles on touch
    await this.refreshProfiles();
    await this.updateDisplay(ev.action);
  }

  private async refreshProfiles(): Promise<void> {
    this.profiles = await listProfiles();
    if (this.selectedIndex >= this.profiles.length) {
      this.selectedIndex = 0;
    }
  }

  private async updateDisplay(action: any): Promise<void> {
    if (this.profiles.length === 0) {
      await action.setTitle("No\nProfiles");
      await action.setFeedback({
        title: "No Profiles",
        value: "Add profiles in XOutputRenew",
      });
      return;
    }

    const profile = this.profiles[this.selectedIndex];
    const status = await getStatus();
    const isRunning = status.isRunning && status.profileName === profile.name;

    // Set title on the key
    await action.setTitle(profile.name);

    // Set feedback for LCD strip (Stream Deck+)
    await action.setFeedback({
      title: profile.name,
      value: isRunning ? "Running" : "Stopped",
      indicator: {
        value: isRunning ? 100 : 0,
        bar_bg_c: isRunning ? "0:#107C10,1:#107C10" : "0:#6C757D,1:#6C757D",
      },
    });

    // Update state
    await action.setState(isRunning ? 1 : 0);
  }
}
