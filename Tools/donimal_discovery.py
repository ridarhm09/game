"""Run on the backend computer: python Tools/donimal_discovery.py --port 8000."""

import argparse
import socket
import time


def main():
    parser = argparse.ArgumentParser(description="Advertise the local DONIMAL API to Unity.")
    parser.add_argument("--port", type=int, default=8000, help="Backend HTTP port")
    parser.add_argument("--udp-port", type=int, default=8888)
    args = parser.parse_args()
    if not (1 <= args.port <= 65535 and 1 <= args.udp_port <= 65535):
        parser.error("Ports must be between 1 and 65535")

    message = f"DONIMAL_PORT:{args.port}".encode("utf-8")
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sender:
        sender.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
        print(f"Advertising DONIMAL HTTP port {args.port} on UDP {args.udp_port}.")
        try:
            while True:
                try:
                    sender.sendto(message, ("255.255.255.255", args.udp_port))
                except OSError as error:
                    print(f"Broadcast failed: {error}; retrying in 2 seconds.")
                time.sleep(2)
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    main()
