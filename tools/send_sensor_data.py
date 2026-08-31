import argparse
import json
import random
import socket
import time


def build_vital_snapshot(args):
    return {
        "HeartRate": args.heart_rate if args.heart_rate is not None else random.uniform(60.0, 100.0),
        "MeanNN": args.mean_nn if args.mean_nn is not None else random.uniform(600.0, 1000.0),
        "SDNN": args.sdnn if args.sdnn is not None else random.uniform(20.0, 80.0),
        "RMSSD": args.rmssd if args.rmssd is not None else random.uniform(20.0, 70.0),
        "PNN50": args.pnn50 if args.pnn50 is not None else random.uniform(0.0, 50.0),
        "hrv": clamp01(args.hrv if args.hrv is not None else random.random()),
    }


def build_vitals_message(args):
    return {
        "Type": "VitalsSnapshot",
        "RequestId": "",
        "Vitals": build_vital_snapshot(args),
    }


def clamp01(value):
    return max(0.0, min(1.0, value))


def main():
    parser = argparse.ArgumentParser(
        description="Send simulated VitalSnapshot messages to Unity's TCP listener."
    )
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8081)
    parser.add_argument("--rate", type=float, default=10.0, help="Frames per second.")
    parser.add_argument("--heart-rate", type=float, help="Fixed heart rate in BPM.")
    parser.add_argument("--mean-nn", type=float, help="Fixed mean NN interval in milliseconds.")
    parser.add_argument("--sdnn", type=float, help="Fixed SDNN in milliseconds.")
    parser.add_argument("--rmssd", type=float, help="Fixed RMSSD in milliseconds.")
    parser.add_argument("--pnn50", type=float, help="Fixed pNN50 percentage.")
    parser.add_argument("--hrv", type=float, help="Fixed normalized HRV focus value from 0 to 1.")
    parser.add_argument(
        "--reconnect-delay",
        type=float,
        default=1.0,
        help="Seconds to wait before reconnecting (default: 1).",
    )
    args = parser.parse_args()

    delay = 1.0 / max(args.rate, 0.1)
    address = (args.host, args.port)

    print(f"Sending TCP VitalSnapshot messages to {args.host}:{args.port}. Press Ctrl+C to stop.")

    while True:
        try:
            with socket.create_connection(address) as sock:
                print("Connected to Unity TCP listener.")

                while True:
                    message = build_vitals_message(args)
                    payload = (json.dumps(message) + "\n").encode("utf-8")
                    sock.sendall(payload)
                    print(message)
                    time.sleep(delay)
        except (ConnectionError, OSError) as error:
            print(f"Connection unavailable ({error}); retrying...")
            time.sleep(max(args.reconnect_delay, 0.1))


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nStopped.")
