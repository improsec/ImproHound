# ImproHound
Identify the attack paths in BloodHound breaking your AD tiering

## Install

**1. Set up your BloodHound database**
1. Collect BloodHound data with [SharpHound](https://bloodhound.readthedocs.io/en/latest/data-collection/sharphound.html) in your AD
 	> Note this will generate noise in your AV, SIEM, etc.
 	
	Example: Run [SharpHound.ps1](https://github.com/BloodHoundAD/BloodHound/blob/master/Collectors/SharpHound.ps1), collect all (yes, GPOLocalGroup is not included in All):

	```
	. .\SharpHound.ps1
	Invoke-BloodHound -CollectionMethod All, GPOLocalGroup
	```
	
	> Tip 1: Use the ```Domain``` parameter to collect data from other domains in the forest.
	
	> Tip 2: To get even more data use [The Session Loop Collection Method](https://bloodhound.readthedocs.io/en/latest/data-collection/sharphound.html#the-session-loop-collection-method) 

1. [Install BloodHound](https://bloodhound.readthedocs.io/en/latest/#install)

1. Upload your BloodHound data in the [BloodHound GUI](https://bloodhound.readthedocs.io/en/latest/data-analysis/bloodhound-gui.html)

**2. Install APOC Neo4j plugin**
(enables awesome graph operations we need) 
1. Download the [APOC](https://github.com/neo4j-contrib/neo4j-apoc-procedures/releases) version matching your Neo4j version (apoc-x.x.x.x-all.jar).
1. Try to remember where you installed Neo4j and place the APOC jar-file under: ```$NEO4J_HOME/plugins/```
	
	* $NEO4J_HOME in Linux: ```/etc/neo4j/```
	* $NEO4J_HOME in Windows: Check the Neo4j service. It will reveal the location.

1. Edit ```$NEO4J_HOME/conf/neo4j.conf``` in your favourite text editor to allow unrestricted APOC access by replacing the line: 
```#dbms.security.procedures.unrestricted=my.extensions.example,my.procedures.*```
with
```dbms.security.procedures.unrestricted=apoc.*```
1. Restart Neo4j

	* Linux: ```systemctl restart neo4j```
	* Windows: ```net stop neo4j && net start neo4j```
	    
**3. Download and run the latest release of ImproHound.exe in Windows (x64)**

&nbsp;&nbsp;&nbsp;&nbsp;Confirm you can log in to the BloodHound DB with the same credentials you use in the BloodHound GUI.

&nbsp;&nbsp;&nbsp;&nbsp;In case you have installed BloodHound on another computer (e.g. Linux VM) make sure allow ingoing TCP on port 7687 on the BloodHound computer.
