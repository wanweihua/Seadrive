# Seadrive background and info
Seadrive is a file hosting service with a specialized transport protocol designed in order to provide reliable data transfer over unreliable network links.

File synchronization- and hosting services is not only an integrated service in
everyday life, but also a powerful tool to support business and organizational
activities. In order to provide users with a transparent experience, the systems
relies on sophisticated mechanisms to create a seamless integration. The
problem with these systems is that they are designed for stable network connections
with a low variety in latency, throughput and loss-rate. The systems
optimized for low bandwidth networks are implemented to work on a small
set of small text-based files, and assumes no prior knowledge of the contents
on the receiver.

Offshore vessels outside the range cellular networks employ a variety of satellite
based communication suites and accommodating physical hardware. These
networks are notorious for having poor upload- and download speed, high loss
rate, poor latency with high variability and are subject to frequent dropped
connections. Furthermore, the fiscal cost associated by using these connections
are high, as the highest performing networks charge per kilobit transferred.
These connections are unsuitable for modern file hosting services, and file
synchronization frameworks, as they never complete synchronizing, often due
to the assignment of new IPs.

Therefore providing the naval fleet with a reliable file-synchronization protocol,
and small in transmission overhead is of the utmost importance. In order to
facilitate the needs for file hosting services, we created a file synchronization
framework, which allows for different deduplication, file-synchronization and
file transportation schemes. The idea was to support a computationally inexpensive
method emphasizing speed over reliability on Local Area Networks,
and a robust but slower methodology for Wide Area Networks.

This repository presents Seadrive- a new file synchronization framework that targets
offshore-based fleets and their land-based counterparts. By utilizing a file synchronization
methodology inspired by binary patch distributions, and creating
a novel reliable application level transport protocol, we are able to successfully
synchronize large files through simulated satellite-based network topologies.
In order to assess the capabilities of our framework, we performed various experiments on the artifacts in the form of micro- and macro benchmarks,
comparing them to both Rsync and Rdiff based protocols.

Our results show that Seadrive is able to produce smaller patches than both
Rsync and Rdiff based protocols, with fewer TCP and application layer requests
necessary, saving up to 10 hours on the slowest network connection and is able
to reliably transfer data through unreliable network topologies.



### OLD README FOR MYSELF

# README

Before trying to run the project there's some things required which needs to be fixed

  - Install Microsoft SQL server (express is fine)
  - Restore the databases using the scripts found in  /APPENDIX c/
  - Fix the connection strings in the projects that contains an APP.config
  - - Point all paths to the path you extract the folder into 
  - Ensure that the SQL server names are interoperable with the Context parameter in SeaDdrive.DAL Context files
  - After running the experiments remember to truncate the tables in the database using TRUNCATE TABLE dbo.TABLENAME;

Use the release configuration. Debug config spawns about 2-300 threads to print all debug info

First, mount the primary server. It will stay running once the application has started. Subsidiary start the Local Server.

C connects to primary server
S initiates the transport protocol