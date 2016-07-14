Seadrive is a file hosting service with a specialized transport protocol designed in order to provide reliable data transfer over unreliable network links.

It is optimized for Satellite based network links with frequent dropped connections, low bandwidth, high packet loss and high ping. In order to provide optimial data transfer rates
we utilize delta-differencing in order to facilitate minimal data required for transportation.
 





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