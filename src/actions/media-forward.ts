import { action, KeyDownEvent, SingletonAction } from '@elgato/streamdeck';
import { seekForward } from '../utils/media-manager';

@action({ UUID: 'ru.valentderah.current-media.media-forward' })
export class MediaForwardAction extends SingletonAction {
	override onKeyDown(ev: KeyDownEvent): void {
		seekForward();
	}
}
