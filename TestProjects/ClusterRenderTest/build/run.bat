start "Patate" ClusterRenderTest.exe -masterNode 0 3  224.0.1.1:25689,25690 -handshakeTimeout 30000 -communicationTimeout 5000 -logFile 0.txt
start "Patate" ClusterRenderTest.exe -node 1 224.0.1.1:25690,25689 -handshakeTimeout 30000 -communicationTimeout 6000 -logFile 1.txt
start "Patate" ClusterRenderTest.exe -node 2 224.0.1.1:25690,25689 -handshakeTimeout 30000 -communicationTimeout 6000 -logFile 2.txt
start "Patate" ClusterRenderTest.exe -node 3 224.0.1.1:25690,25689 -handshakeTimeout 30000 -communicationTimeout 6000 -logFile 3.txt