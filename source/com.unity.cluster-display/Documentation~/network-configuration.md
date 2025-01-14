[Contents](TableOfContents.md) | [Home](index.md) > Network configuration

# Network Configuration

Set up your Cluster Display Hardware elements according to the following
graphical representation (example for a 2x2 screen matrix):

![Cluster display setup example](images/cluster-display-setup-example.png)

To summarize:

* A node in the cluster consists of a workstation and a display output.
* There is one Emitter Node and multiple repeater nodes (here, 1 emitter and 3 repeaters).
* You can set up the emitter as a rendering node to optimize the use of your hardware resources.
* The repeater nodes connect to the Emitter Node via a wired Local Area Network.

Additional recommendations:

* Use an enterprise-grade switch. Communication is done via UDP multicast, which requires a large bandwidth.
* Do not use routing. The UDP packets are sent with TTL1 (time to live), which prevents routing from working.

## Adding Metric on Network interface used by Cluster

If the servers you are using in the Cluster have multiple Network Interface Controllers connected, you must set Metrics on the Network Interface used by the Cluster in order to make sure that all nodes on the Cluster use the same Network Interface to communicate. To do this in Windows 10, follow these steps **for all nodes in the cluster**:

1. Open the Control Panel.
2. Right-click on the network interface being used for Cluster messaging:

    ![Network Interface properties](images/network-interface-properties.png)

3. Click **Properties** in the context menu.
4. Once the Properties window is open, select **Internet Protocol Version 4** and click **Properties.**
5. Select **Advanced** at the bottom right. In the **Advanced TCP/IP Setting**, enter “1” in the **Interface metric** field.

    ![Network Interface metrics](images/network-interface-metric.png)

## Disabling Multimedia Class Scheduler Service network throttling

Windows version of Unity uses Multimedia Class Scheduler Service (MMCSS) to ensure it runs smoothly and is minimally impacted by other tasks running on the computer.  To achieve this Windows throttles down the network traffic when multimedia applications (like Unity applications) are running.  This however has the unfortunate effect of increasing latency of communication between the emitter and repeaters which in turn can reduce the frame rate.  To work around this problem it is strongly recommended to disable the network throttling by setting the `NetworkThrottlingIndex` key in `Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile` key to 0xFFFFFFFF (instead of the default value of 10) and rebooting.  This should normally be done automatically during the MissionControl installation.
