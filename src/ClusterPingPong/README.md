# Akka.ClusterPingPong
This project is meant to serve as a distributed benchmark for clocking the performance of 2+ [Akka.Cluster](https://getakka.net/articles/clustering/cluster-overview.html) nodes echo-ing messages back and forth at high rates.

## Abstract
This benchmark is effective for measuring:

1. The throughput of a single Akka.Remote connection inside a real-world environment (Docker or Kubernetes);
2. A distributed network of Akka.NET nodes running in a single machine or across multiple machines (with Kubernetes); and
3. The impact that real tooling differences, such as choice of dispatcher or serializer, has on the cumulative throughput of an entire network - where multiple nodes may all be running on the same CPUs and hardware resources.

The goal of this benchmark is the establish the type of performance you might realistically expect inside an Akka.NET application hosted inside a cloud on top of commodity hardware.

## Running ClusterPingPong
This benchmark is run using Docker and `docker-compose` or Kubernetes.

First, build all of the benchmark images by running the following command at the root of the repository directory:

```
PS> ./build.cmd docker
```

### Via `docker-compose`

In the [`/src/ClusterPingPong/src/docker`](docker/) folder you can deploy the sample by running the following command:

```
PS> docker-compose up
```

This will launch a minimum size 2-node cluster.

> NOTE: all nodes in the cluster will terminate at the conclusion of the benchmark and their Docker containers will stop.

To launch a larger cluster, simply add more "participant" nodes:

```
PS> docker-compose up --scale participants=2
```

This will run a 3 node cluster (2 participants, 1 seed node).

### Via Kubernetes
In the [`/src/ClusterPingPong/src/k8s`](k8s/) folder you can deploy the sample by running the following command:

```
PS> kubectl apply -f ./clusterpingpong.yml
```

This will deploy a 2-node ClusterPingPong cluster into a dedicated `pingpong` namespace.

You can view the logs from the leader node, which reports the benchmark results, via the following `kubectl` command:

```
PS> kubectl -n pingpong logs pingpong-0 -f
```

You can scale up the benchmark to run on a larger number of pods via:

```
PS> kubectl -n pingpong scale statefulset/pingpong --replicas 3
```

This will scale the network up to a 3 node cluster.