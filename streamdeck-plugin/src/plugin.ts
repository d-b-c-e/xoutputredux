import streamDeck, { LogLevel } from "@elgato/streamdeck";

import { StartProfileAction } from "./actions/start-profile";
import { StopProfileAction } from "./actions/stop-profile";
import { ToggleProfileAction } from "./actions/toggle-profile";
import { StartMonitoringAction, StopMonitoringAction, ToggleMonitoringAction } from "./actions/monitoring";
import { LaunchAppAction } from "./actions/launch-app";
import { ProfileDialAction } from "./actions/profile-dial";

// Set up logging
streamDeck.logger.setLevel(LogLevel.DEBUG);

// Register profile actions
streamDeck.actions.registerAction(new StartProfileAction());
streamDeck.actions.registerAction(new StopProfileAction());
streamDeck.actions.registerAction(new ToggleProfileAction());

// Register monitoring actions
streamDeck.actions.registerAction(new StartMonitoringAction());
streamDeck.actions.registerAction(new StopMonitoringAction());
streamDeck.actions.registerAction(new ToggleMonitoringAction());

// Register utility actions
streamDeck.actions.registerAction(new LaunchAppAction());

// Register encoder actions
streamDeck.actions.registerAction(new ProfileDialAction());

// Connect to Stream Deck
streamDeck.connect();
