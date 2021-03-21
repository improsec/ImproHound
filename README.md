# ImproHound
Identify the attack paths in BloodHound breaking your AD tiering

ImproHound is a dotnet GUI program compiled to a standalone win x64 exe. 
To use ImproHound, you must run SharpHound to collect the necessary data from the AD. You will then upload the data to your BloodHound installation. ImproHound will connect to the underlying Neo4j database of BloodHound. In ImproHound, you will categorize the AD into tiers via the OU structure, and ImproHound will identify the AD relations that enable AD objects to compromise an object of a higher (closer to zero) tier and save the tiering violations in a csv file.

## Install

**1. Set up your BloodHound database**
1. [Install BloodHound](https://bloodhound.readthedocs.io/en/latest/#install)

1. Collect BloodHound data with [SharpHound](https://bloodhound.readthedocs.io/en/latest/data-collection/sharphound.html) in your AD
 	> Note this will generate noise in your AV, SIEM, etc.
 	
	Example: Run [SharpHound.ps1](https://github.com/BloodHoundAD/BloodHound/blob/master/Collectors/SharpHound.ps1), collect all (yes, GPOLocalGroup is not included in All):

	```
	. .\SharpHound.ps1
	Invoke-BloodHound -CollectionMethod All, GPOLocalGroup
	```
	
	> Tip 1: Use the ```Domain``` parameter to collect data from other domains in the forest.
	
	> Tip 2: To get even more data use [The Session Loop Collection Method](https://bloodhound.readthedocs.io/en/latest/data-collection/sharphound.html#the-session-loop-collection-method) 

1. Upload your BloodHound data in the [BloodHound GUI](https://bloodhound.readthedocs.io/en/latest/#import-and-explore-the-data)

**2. Install APOC Neo4j plugin**
(enables awesome graph operations we need) 
1. Download the [APOC](https://github.com/neo4j-contrib/neo4j-apoc-procedures/releases) version matching your Neo4j version (apoc-x.x.x.x-all.jar).
1. Try to remember where you installed Neo4j and place the APOC jar-file under: ```$NEO4J_HOME/plugins/```
	
	* $NEO4J_HOME in Linux: ```/var/lib/neo4j/```
	* $NEO4J_HOME in Windows: Check the Neo4j service. It will reveal the location.

1. Edit ```neo4j.conf``` in your favourite text editor to allow unrestricted APOC access by replacing the line: 
```#dbms.security.procedures.unrestricted=my.extensions.example,my.procedures.*```
with
```dbms.security.procedures.unrestricted=apoc.*```

	* neo4j.conf in Linux: ```/etc/neo4j/neo4j.conf```
	* neo4j.conf in Windows: ```$NEO4J_HOME/conf/neo4j.conf```

	> If you want to run ImproHound on one host and BloodHound on another, you have to allow remote connections to the Neo4j database on the BloodHound host. To do so remove # from the line ```#dbms.default_listen_address=0.0.0.0``` in ```neo4j.conf```.

1. Restart Neo4j

	* Linux: ```systemctl restart neo4j```
	* Windows: ```net stop neo4j && net start neo4j```
	    
**3. Download and run the latest release of ImproHound.exe in Windows (x64)**

&nbsp;&nbsp;&nbsp;&nbsp;Confirm you can log in to the BloodHound DB with the same credentials you use in the BloodHound GUI.

## Usage

### Connect to database
![alt text](https://github.com/improsec/ImproHound/blob/master/readme-images/01-connect.png?raw=true)

Enter the database credentials and establish a connection. It is the same credentials you use in BloodHound GUI.

### Continue or start over
![alt text](https://github.com/improsec/ImproHound/blob/master/readme-images/02-continue-startover.png?raw=true)

ImproHound creates a ‘TierX’ label on nodes in the BloodHound database. If you have used ImproHound before with this BloodHound database, you will be asked if you want to continue with the tiering you have already created or if you want to start over.

### OU structure
![alt text](https://github.com/improsec/ImproHound/blob/master/readme-images/03-ou-structure.png?raw=true)

This is the page where you will categorize the AD objects into tiers. The window displays the OU structure. Each AD object has a Tier value which can be increased and decreased with the arrows.

**Set children to tier**

If you select a domain or an AD container, you can click ‘Set children to tier’ to set all the children to the Tier level of the given domain/container recursively.

**Set tier for GPOs**

If you click ‘Set tier for GPOs’ each GPO will have their tier level set to the tier level of the OU with highest tier (closest to zero) which the GPO is linked to. GPOs not linked to an OU will not have their tier level changed.

The tier levels will be saved in the BloodHound first, if not already done.

**Get tiering violations**

Find all relations in the BloodHound database where an AD object has control of an AD object from a higher tier (closer to zero). The tier violations will be written to a csv file. Another csv file will be created with all AD objects and their tier levels. 

The tier levels will be saved in the BloodHound first, if not already done.

**Reset**

All tier levels will be set to 1 in ImproHound. All ‘TierX’ labels in the BloodHound database will be removed.

**Save**

Save the tier levels as a ‘TierX’ label in the BloodHound database. 
