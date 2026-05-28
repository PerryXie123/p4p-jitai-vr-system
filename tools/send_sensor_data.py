import argparse
import json
import random
import socket
import time


def clamp01(value):
    return max(0.0, min(1.0, value))


def main():
    parser = argparse.ArgumentParser(
        description="Send simulated HRV and eye-gaze frames to Unity DataReceiverScript."
    )
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5005)
    parser.add_argument("--rate", type=float, default=10.0, help="Frames per second.")
    parser.add_argument("--hrv", type=float, help="Fixed HRV value from 0 to 1.")
    parser.add_argument("--eye-gaze", type=float, help="Fixed eye-gaze value from 0 to 1.")
    args = parser.parse_args()

    delay = 1.0 / max(args.rate, 0.1)
    address = (args.host, args.port)

    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
        print(f"Sending sensor frames to {args.host}:{args.port}. Press Ctrl+C to stop.")

        while True:
            frame = {
                "hrv": clamp01(args.hrv if args.hrv is not None else random.random()),
                "eyeGaze": clamp01(args.eye_gaze if args.eye_gaze is not None else random.random()),
            }

            payload = json.dumps(frame).encode("utf-8")
            sock.sendto(payload, address)
            print(frame)
            time.sleep(delay)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nStopped.")
