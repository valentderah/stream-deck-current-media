import { action, DialAction, DidReceiveSettingsEvent, KeyAction, KeyDownEvent, SingletonAction, WillAppearEvent, WillDisappearEvent } from '@elgato/streamdeck';
import { execFile } from 'child_process';
import path from 'path';
import { Marquee } from '../utils/marquee';

type MediaInfo = {
	Title?: string;
	Artist?: string;
	Artists?: string[];
	AlbumArtist?: string;
	AlbumTitle?: string;
	Status?: 'Playing' | 'Paused' | 'Stopped';
	CoverArtBase64?: string;
};

type MediaInfoSettings = {
	showTitle?: boolean;
	showArtists?: boolean;
};

@action({ UUID: 'ru.valentderah.media-manager.media-info' })
export class MediaInfoAction extends SingletonAction<MediaInfoSettings> {
	private static readonly UPDATE_INTERVAL_MS = 1000;
	private static readonly MARQUEE_INTERVAL_MS = 1000;
	private static readonly MARQUEE_MAX_LENGTH = 16;
	private static readonly MARQUEE_SEPARATOR = '   ';


	private static readonly DEFAULT_SETTINGS: MediaInfoSettings = {
		showTitle: true,
		showArtists: true
	};
	private static readonly HELPER_EXE_NAME = 'MediaManagerHelper.exe';
	private static readonly ERROR_MESSAGES = {
		FILE_NOT_FOUND: 'Error\nFile Not\nFound',
		HELPER_ERROR: 'Error\nHelper',
		PARSING_ERROR: 'Error\nParsing',
		NOTHING_PLAYING: 'Nothing\nPlaying'
	} as const;

	private intervalId: NodeJS.Timeout | undefined;
	private marqueeIntervalId: NodeJS.Timeout | undefined;
	private currentAction: DialAction<MediaInfoSettings> | KeyAction<MediaInfoSettings> | undefined;
	private readonly titleMarquee: Marquee;
	private currentMediaInfo: MediaInfo | null = null;
	private settings: MediaInfoSettings = { ...MediaInfoAction.DEFAULT_SETTINGS };

	constructor() {
		super();
		this.titleMarquee = new Marquee(
			MediaInfoAction.MARQUEE_MAX_LENGTH,
			MediaInfoAction.MARQUEE_SEPARATOR
		);
	}

	override async onWillAppear(ev: WillAppearEvent<MediaInfoSettings>): Promise<void> {
		this.currentAction = ev.action;
		await this.loadSettings(ev.action);
		await this.updateMediaInfo(ev.action);
		this.startUpdateInterval();
		if (this.settings.showTitle) {
			this.startMarquee();
		}
	}

	override onWillDisappear(ev: WillDisappearEvent<MediaInfoSettings>): void | Promise<void> {
		this.stopUpdateInterval();
		this.stopMarquee();
		this.currentAction = undefined;
		this.currentMediaInfo = null;
	}

	override async onKeyDown(ev: KeyDownEvent<MediaInfoSettings>): Promise<void> {
		await this.updateMediaInfo(ev.action);
	}

	override async onDidReceiveSettings(ev: DidReceiveSettingsEvent<MediaInfoSettings>): Promise<void> {
		const wasTitleEnabled = this.settings.showTitle;
		this.settings = {
			showTitle: ev.payload.settings.showTitle ?? MediaInfoAction.DEFAULT_SETTINGS.showTitle,
			showArtists: ev.payload.settings.showArtists ?? MediaInfoAction.DEFAULT_SETTINGS.showArtists
		};

		if (this.settings.showTitle && !wasTitleEnabled) {
			this.startMarquee();
		} else if (!this.settings.showTitle && wasTitleEnabled) {
			this.stopMarquee();
		}

		if (this.currentAction) {
			await this.updateMarqueeTitle(this.currentAction);
		}
	}

	private async loadSettings(action: DialAction<MediaInfoSettings> | KeyAction<MediaInfoSettings>): Promise<void> {
		const loadedSettings = await action.getSettings();
		this.settings = {
			showTitle: loadedSettings.showTitle ?? MediaInfoAction.DEFAULT_SETTINGS.showTitle,
			showArtists: loadedSettings.showArtists ?? MediaInfoAction.DEFAULT_SETTINGS.showArtists
		};
	}

	private getHelperPath(): string {
		return path.join(process.cwd(), 'bin', MediaInfoAction.HELPER_EXE_NAME);
	}

	private async checkHelperExists(helperPath: string): Promise<boolean> {
		try {
			const fs = await import('fs/promises');
			await fs.access(helperPath);
			return true;
		} catch {
			console.error(`${MediaInfoAction.HELPER_EXE_NAME} not found at: ${helperPath}`);
			console.error('Please build the C# project using: cd MediaManagerHelper && build.bat');
			return false;
		}
	}

	private async handleHelperError(
		action: DialAction<MediaInfoSettings> | KeyAction<MediaInfoSettings>,
		error: Error & { code?: string | number | null },
		helperPath: string
	): Promise<void> {
		if (error.code === 'ENOENT') {
			console.error(`${MediaInfoAction.HELPER_EXE_NAME} not found at: ${helperPath}`);
			console.error('Please build the C# project and copy MediaManagerHelper.exe to the bin folder.');
			await action.setTitle(MediaInfoAction.ERROR_MESSAGES.FILE_NOT_FOUND);
		} else {
			const codeStr = error.code != null ? ` (code: ${error.code})` : '';
			console.error(`Helper error: ${error.message}${codeStr}`);
			await action.setTitle(MediaInfoAction.ERROR_MESSAGES.HELPER_ERROR);
		}
	}

	private async processMediaInfo(
		action: DialAction<MediaInfoSettings> | KeyAction<MediaInfoSettings>,
		stdout: string
	): Promise<void> {
		try {
			const info: MediaInfo = JSON.parse(stdout);

			if (info.CoverArtBase64) {
				await action.setImage(`data:image/png;base64,${info.CoverArtBase64}`);
			}

			const titleChanged = this.currentMediaInfo?.Title !== info.Title;
			this.currentMediaInfo = info;

			if (titleChanged && info.Title) {
				this.titleMarquee.setText(info.Title);
			}

			await this.updateMarqueeTitle(action);
		} catch (parseError) {
			console.error('Failed to parse JSON from helper:', parseError);
			await action.setTitle(MediaInfoAction.ERROR_MESSAGES.PARSING_ERROR);
		}
	}

	private async updateMediaInfo(action: DialAction<MediaInfoSettings> | KeyAction<MediaInfoSettings>): Promise<void> {
		const helperPath = this.getHelperPath();

		if (!(await this.checkHelperExists(helperPath))) {
			await action.setTitle(MediaInfoAction.ERROR_MESSAGES.FILE_NOT_FOUND);
			return;
		}

		return new Promise<void>((resolve, reject) => {
			execFile(helperPath, (error, stdout, stderr) => {
				(async () => {
					try {
						if (error) {
							await this.handleHelperError(action, error, helperPath);
							resolve();
							return;
						}

						if (stderr) {
							resolve();
							return;
						}

						if (stdout) {
							await this.processMediaInfo(action, stdout);
						} else {
							this.currentMediaInfo = null;
							this.titleMarquee.setText('Nothing Playing');
							await action.setTitle(MediaInfoAction.ERROR_MESSAGES.NOTHING_PLAYING);
						}
						resolve();
					} catch (err) {
						reject(err);
					}
				})();
			});
		});
	}

	private startUpdateInterval(): void {
		this.stopUpdateInterval();
		this.intervalId = setInterval(() => {
			if (this.currentAction) {
				this.updateMediaInfo(this.currentAction);
			}
		}, MediaInfoAction.UPDATE_INTERVAL_MS);
	}

	private stopUpdateInterval(): void {
		if (this.intervalId) {
			clearInterval(this.intervalId);
			this.intervalId = undefined;
		}
	}

	private startMarquee(): void {
		this.stopMarquee();
		this.marqueeIntervalId = setInterval(() => {
			if (this.currentAction && this.currentMediaInfo) {
				this.updateMarqueeTitle(this.currentAction);
			}
		}, MediaInfoAction.MARQUEE_INTERVAL_MS);
	}

	private stopMarquee(): void {
		if (this.marqueeIntervalId) {
			clearInterval(this.marqueeIntervalId);
			this.marqueeIntervalId = undefined;
		}
	}

	private getArtistText(info: MediaInfo): string {
		if (info.Artists && info.Artists.length > 0) {
			return info.Artists.join(', ');
		}
		return info.Artist || '';
	}

	private buildDisplayText(info: MediaInfo): string | null {
		const parts: string[] = [];

		if (this.settings.showArtists) {
			const artistText = this.getArtistText(info);
			if (artistText) {
				parts.push(artistText);
			}
		}

		if (this.settings.showTitle && info.Title) {
			parts.push(this.titleMarquee.getCurrentFrame());
		}

		return parts.length > 0 ? parts.join('\n') : null;
	}

	private async updateMarqueeTitle(action: DialAction<MediaInfoSettings> | KeyAction<MediaInfoSettings>): Promise<void> {
		if (!this.currentMediaInfo) {
			await action.setTitle('');
			return;
		}

		const displayText = this.buildDisplayText(this.currentMediaInfo);
		await action.setTitle(displayText || '');
	}
}