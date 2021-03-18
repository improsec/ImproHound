# ImproHound
Identify the attack paths in BloodHound breaking your AD tiering

## Install ImproHound in Windows (x64)

**1. Set up your BloodHound database**
1. Install BloodHound for Windows: https://bloodhound.readthedocs.io/en/latest/installation/windows.html
1. Run SharpHound in your AD 
(https://bloodhound.readthedocs.io/en/latest/data-collection/sharphound.html)
1. Upload your data collected with SharpHound in the BloodHound GUI
(https://bloodhound.readthedocs.io/en/latest/data-analysis/bloodhound-gui.html)

**2. Install APOC Neo4j plugin**
(enables awesome graph operations we need) 
1. Download the APOC version matching your Neo4j version (apoc-x.x.x.x-all.jar): https://github.com/neo4j-contrib/neo4j-apoc-procedures/releases
1. Try to remember where you installed Neo4j and place the APOC jar-file under: ```$NEO4J_HOME\plugins\``` 
1. Edit ```$NEO4J_HOME\conf\neo4j.conf``` in your favourite text editor to allow unrestricted APOC access by replacing the line: 
```#dbms.security.procedures.unrestricted=my.extensions.example,my.procedures.*```
with
```dbms.security.procedures.unrestricted=apoc.*```
1. Restart Neo4j

	A Neo4j service has been created if you followed the BloodHound installation guide. Go and restart that service.
	    
**3. Download and run the latest release of ImproHound.exe**

&nbsp;&nbsp;&nbsp;&nbsp;Confirm you can log in to the BloodHound DB with the same credentials you use in the BloodHound GUI.
