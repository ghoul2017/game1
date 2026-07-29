#!/usr/bin/env python3
import argparse
import socketserver
import threading
from datetime import datetime, timezone


def timestamp():
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds")


class EchoHandler(socketserver.BaseRequestHandler):
    def handle(self):
        server = self.server
        with server.counter_lock:
            server.connection_count += 1
            connection = server.connection_count

        print(
            f"P0_TCP_ECHO_CONNECT utc={timestamp()} connection={connection} "
            f"remote={self.client_address[0]}:{self.client_address[1]}",
            flush=True,
        )

        received_bytes = 0
        receive_count = 0
        while True:
            data = self.request.recv(65536)
            if not data:
                break

            self.request.sendall(data)
            received_bytes += len(data)
            receive_count += 1
            print(
                f"P0_TCP_ECHO_DATA utc={timestamp()} connection={connection} "
                f"receive={receive_count} bytes={len(data)} total_bytes={received_bytes}",
                flush=True,
            )

        print(
            f"P0_TCP_ECHO_CLOSE utc={timestamp()} connection={connection} "
            f"receives={receive_count} total_bytes={received_bytes}",
            flush=True,
        )


class EchoServer(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True

    def __init__(self, address):
        super().__init__(address, EchoHandler)
        self.counter_lock = threading.Lock()
        self.connection_count = 0


def main():
    parser = argparse.ArgumentParser(
        description="Task-scoped LAN TCP echo for the game1 P0 platform smoke"
    )
    parser.add_argument("--bind", default="0.0.0.0")
    parser.add_argument("--port", type=int, required=True)
    args = parser.parse_args()

    with EchoServer((args.bind, args.port)) as server:
        print(
            f"P0_TCP_ECHO_READY utc={timestamp()} bind={args.bind} port={args.port}",
            flush=True,
        )
        server.serve_forever(poll_interval=0.25)


if __name__ == "__main__":
    main()
