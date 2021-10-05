set LOCAL=%~dp0
kubectl apply -f "%~dp0/clusterbootstrap_k8s.yaml"
kubectl get all -n clusterbootstrap