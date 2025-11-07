# Copyright 2015 gRPC authors.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
"""The Python implementation of the GRPC helloworld.Greeter client."""

from __future__ import print_function

import logging

import grpc
import helloworld_pb2
import helloworld_pb2_grpc


def run():
    # NOTE(gRPC Python Team): .close() is possible on a channel and should be
    # used in circumstances in which the with statement does not fit the needs
    # of the code.
    print("Will try to greet world ...")
    # credential = grpc.ssl_channel_credentials(open('localhost.pem', 'rb').read())
    # with grpc.secure_channel('localhost:7080', credential) as channel:
    with grpc.insecure_channel("localhost:5071") as channel:
        stub = helloworld_pb2_grpc.GreeterStub(channel)
        response = stub.RequestInstruction(helloworld_pb2.InputStatus(x=0, z=0, speed=100, direction=180, distance=0))
        print("Greeter client received: action=" + str(response.action))


if __name__ == "__main__":
    logging.basicConfig()
    run()

