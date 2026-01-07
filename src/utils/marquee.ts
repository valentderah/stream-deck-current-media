export class Marquee {
	private position: number = 0;
	private text: string = '';
	private readonly maxLength: number;
	private readonly separator: string;
	private intervalId: NodeJS.Timeout | undefined;
	private updateCallback: (() => void) | undefined;

	constructor(maxLength: number = 20, separator: string = '   ') {
		this.maxLength = maxLength;
		this.separator = separator;
	}

	setText(text: string): void {
		if (this.text !== text) {
			this.text = text;
			this.position = 0;
		}
	}

	getCurrentFrame(): string {
		if (!this.text || this.text.length <= this.maxLength) {
			return this.text;
		}

		const extendedText = this.text + this.separator + this.text;
		const endPosition = this.position + this.maxLength;
		const frame = extendedText.substring(this.position, endPosition);

		this.position = (this.position + 1) % (this.text.length + this.separator.length);

		return frame;
	}

	start(intervalMs: number, callback: () => void): void {
		this.stop();
		this.updateCallback = callback;
		this.intervalId = setInterval(() => {
			if (this.updateCallback) {
				this.updateCallback();
			}
		}, intervalMs);
	}

	stop(): void {
		if (this.intervalId) {
			clearInterval(this.intervalId);
			this.intervalId = undefined;
		}
		this.updateCallback = undefined;
	}

	reset(): void {
		this.position = 0;
	}

	getText(): string {
		return this.text;
	}

	isRunning(): boolean {
		return this.intervalId !== undefined;
	}
}

export function createMarquee(maxLength: number = 20, separator: string = '   '): Marquee {
	return new Marquee(maxLength, separator);
}

