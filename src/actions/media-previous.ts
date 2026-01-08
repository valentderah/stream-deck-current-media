import { action, KeyDownEvent, SingletonAction } from '@elgato/streamdeck';
import { previousMedia } from '../utils/media-manager';

@action({ UUID: 'ru.valentderah.media-manager.media-previous' })
export class MediaPreviousAction extends SingletonAction {
	override onKeyDown(ev: KeyDownEvent): void {
		previousMedia();
	}
}
