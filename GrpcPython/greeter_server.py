# greeter_server.py
from concurrent import futures
import logging
import grpc
import helloworld_pb2
import helloworld_pb2_grpc
import time

import gymnasium
from gymnasium import spaces
import numpy as np
from stable_baselines3 import PPO
import os
from stable_baselines3.common.callbacks import EvalCallback, StopTrainingOnRewardThreshold, CheckpointCallback


class Greeter(helloworld_pb2_grpc.GreeterServicer):
    def __init__(self):
        self.current_action = np.zeros((2,), dtype=np.float32)
        # self.current_action = spaces.Box(low=-1.0, high=1.0, shape=(2,), dtype=np.float32)
        self.last_request = None
        self.done = False
        self.reward = 0

    def RequestInstruction(self, request, context):
        self.last_request = request
        self.reward = request.distance  # distance is the distance traveled along the spline
        #print(request.speed)
        self.done = request.done
        # print (self.current_action)
        return helloworld_pb2.OutputAI(
            action=self.current_action.tolist(),
            # action=[1,1],
            reward=self.reward,
            done=self.done
        )

def serve():
    port = "5078"
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    greeter = Greeter()
    helloworld_pb2_grpc.add_GreeterServicer_to_server(greeter, server)
    server.add_insecure_port("[::]:" + port)
    server.start()
    print("Server started, listening on " + port)
    return greeter, server





class UnityGymControllerEnv(gymnasium.Env):
    def __init__(self, greeter):
        super().__init__()
        # self.action_space = spaces.Discrete(7)
        self.action_space = spaces.Box(low=-1.0, high=1.0, shape=(2,), dtype=np.float32)
        self.observation_space = spaces.Box(low=-np.inf, high=np.inf, shape=(6,), dtype=np.float32)
        self.state = np.zeros(6, dtype=np.float32)
        self.greeter = greeter

    def reset(self, seed=None):
        self.state = np.zeros(6, dtype=np.float32)
        self.greeter.done = False
        self.greeter.current_action = np.zeros(2, dtype=np.float32)
        self.greeter.last_request = None
        return self.state, {}   #leave info as empty

    def step(self, action):
        # the next return value
        # print("action: ", action)
        self.greeter.current_action = action
        # wait for Unity
        self.greeter.done = False
        while self.greeter.last_request is None:
            time.sleep(0.000001)
        # get the latest request data from Unity
        request = self.greeter.last_request
        self.greeter.last_request = None
        # update
        self.state = np.array([request.x, request.z, request.speed, request.direction, request.distance, request.distanceLeft], dtype=np.float32)
        # reward and done flag
        reward = self.greeter.reward

        # in newer versions of openAI gymnasium, both 'terminated' and 'truncated' are returned, we just combine them and call them 'done'.
        return self.state, reward, self.greeter.done, self.greeter.done, {}

    def render(self, mode='human'):
        print("Go to Unity!!")

    def close(self):
        pass




def trainMyModel():
    PPO_path = os.path.join('Saved Models', 'PPO_model_track17', 'best_model.zip')
    env = UnityGymControllerEnv(greeter)
    # model = PPO("MlpPolicy", env, verbose=1, n_steps = 2048, ent_coef = 0.02)
    model = PPO.load(PPO_path, env=env, device="cpu", n_steps = 2048, batch_size = 64, ent_coef = 0.01, gamma=0.9999, gae_lambda=0.98)  # load my trained model
    eval_callback = EvalCallback(
        env,
        best_model_save_path=os.path.join('Saved Models', 'PPO_model_track18'),
        log_path=os.path.join('Saved Models', 'PPO_model_track18'),
        eval_freq=10000,  # Evaluate every N steps
        n_eval_episodes=5,  # Run 5 eval episodes each time
        deterministic=True,
        render=False
    )

    # save every 2000 steps
    checkpoint_callback = CheckpointCallback(
        save_freq=500000,  # Save every 500000 steps
        save_path=os.path.join('Saved Models', 'PPO_model_track18'),
        name_prefix="PPO_18_checkpoint"
    )
    callback = [eval_callback, checkpoint_callback]
    model.learn(total_timesteps=20000000, callback=callback)
    model.save(os.path.join('Saved Models', 'PPO_model_track18'))
    print("Training finished. Model saved.")





def runMyModel():
    PPO_path = os.path.join('Saved Models', 'lap1_model.zip')
    env = UnityGymControllerEnv(greeter)
    model = PPO.load(PPO_path, env=env, device="cpu")  # load my trained model
    total_reward = 0
    done = False
    state, info = env.reset()
    while not done:
        # action = action.item()
        action, _ = (model.predict(state, deterministic=True))
        state, reward, terminated, truncated, info = env.step(action)
        done = terminated or truncated

        total_reward += reward

    print("Test run total reward:", total_reward)





if __name__ == "__main__":
    logging.basicConfig()
    greeter, server = serve()

    # trainMyModel()

    runMyModel()












'''
from concurrent import futures
import logging

import grpc
import helloworld_pb2
import helloworld_pb2_grpc
import random


class Greeter(helloworld_pb2_grpc.GreeterServicer):
    def RequestInstruction(self, request, context):
        print("received request: " + str(request.x) + ", " + str(request.z) + ", " + str(request.speed) + ", " + str(request.direction))

        action = random.randint(0, 3)

        # Simple reward: positive if moving fast, negative if slow
        reward = request.speed / 10.0
        done = False  # could set to True if car crashes or out of bounds

        return helloworld_pb2.OutputAI(action=action, reward=reward, done=done)
def serve():
    port = "5071"
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    helloworld_pb2_grpc.add_GreeterServicer_to_server(Greeter(), server)
    server.add_insecure_port("[::]:" + port)
    server.start()
    print("Server started, listening on " + port)
    server.wait_for_termination()


if __name__ == "__main__":
    logging.basicConfig()
    serve()
'''


























