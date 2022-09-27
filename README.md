# ImproHound
Identify the attack paths in BloodHound breaking your AD tiering

ImproHound is a dotnet standalone win x64 exe with GUI. 
To use ImproHound, you must run SharpHound to collect the necessary data from the AD. You will then upload the data to your BloodHound installation. ImproHound will connect to the underlying Neo4j database of BloodHound. In ImproHound, you will categorize the AD into tiers via the OU structure, and ImproHound will identify the AD relations that enable AD objects to compromise an object of a higher (closer to zero) tier and save the tiering violations in a csv file.

ImproHound blogpost: https://improsec.com/tech-blog/improhound-identify-ad-tiering-violations

ImproHound demo video: https://www.youtube.com/watch?v=zQ01tvTr6H0 

ImproHound DEF CON Adversary Village presentation: https://www.youtube.com/watch?v=MTsPTI7OoqM


##### Table of Contents  
* [Install](#install)
* [Usage](#usage)
* [Guidelines for tiering AD objects](#guidelines-for-tiering-ad-objects)
* [Latest tested version of dependencies](#latest-tested-version-of-dependencies)

## Install

**1. Set up your BloodHound database**
1. [Install BloodHound](https://bloodhound.readthedocs.io/en/latest/#install)

1. Collect BloodHound data with [SharpHound](https://bloodhound.readthedocs.io/en/latest/data-collection/sharphound.html) in your AD
 	> Note this will generate noise in your AV, SIEM, etc.
 	
	Example: Run [SharpHound.exe](https://github.com/BloodHoundAD/BloodHound/blob/master/Collectors/SharpHound.exe) from cmd, collect all (yes, GPOLocalGroup is not included in All):

	```
	SharpHound.exe --CollectionMethods All,GPOLocalGroup
	```
	
	> Tip 1: Use the ```Domain``` parameter to collect data from other domains in the forest.
	
	> Tip 2: To get even more data use [The Session Loop Collection Method](https://bloodhound.readthedocs.io/en/latest/data-collection/sharphound.html#the-session-loop-collection-method) 

1. Upload your BloodHound data in the [BloodHound GUI](https://bloodhound.readthedocs.io/en/latest/#import-and-explore-the-data)
	> There is a bug in BloodHound that sometimes cause the domain json file to be skipped when uploading a BloodHound data zip. Check the DB stats in BloodHound after data upload and make sure the domain objects exist.  
 	
**2. Install APOC Neo4j plugin**
(enables awesome graph operations we need) 
1. Download the [APOC](https://github.com/neo4j-contrib/neo4j-apoc-procedures/releases) version matching your Neo4j version (apoc-x.x.x.x-all.jar).

	Find the APOC version matching your Neo4j version in the [version compatibility matrix](https://github.com/neo4j-contrib/neo4j-apoc-procedures#version-compatibility-matrix). 

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
	* Windows (cmd): ```net stop neo4j && net start neo4j``` (PowerShell: ```net stop neo4j; net start neo4j```) 
	    
**3. Download and run the latest release of ImproHound.exe in Windows (x64)**

&nbsp;&nbsp;&nbsp;&nbsp;Confirm you can log in to the BloodHound DB with the same credentials you use in the BloodHound GUI.

## Usage

### Connect to database
![alt text](https://github.com/improsec/ImproHound/blob/master/readme-images/01-connect.png?raw=true)

Enter the database credentials and establish a connection. It is the same credentials you use in BloodHound GUI.

### Continue or start over
![alt text](https://github.com/improsec/ImproHound/blob/master/readme-images/02-continue-startover.png?raw=true)

ImproHound creates a ‘TierX’ label on nodes in the BloodHound database. If you have used ImproHound before with this BloodHound database, you will be asked if you want to continue with the tiering you have already created or if you want to start over.

### Default tiering or all in Tier 2
![alt text](https://github.com/improsec/ImproHound/blob/master/readme-images/03-default-or-tier2.png?raw=true)

ImproHound gives you the option to set 'Default tiering' which will set Domain Admins in Tier 0, Domain Users in Tier 2, etc, or set all objects in Tier 2. 

### OU structure
![alt text](https://github.com/improsec/ImproHound/blob/master/readme-images/04-ou-structure.png?raw=true)

This is the page where you will categorize the AD objects into tiers. The window displays the OU structure. Each AD object has a tier value which can be increased and decreased with the arrows.

**Set children to tier**

If you select a domain or an AD container, you can click ‘Set children to tier’ to set all children (recursively) to the tier level of the given domain/container.

**Set members to tier**

If you select a group, you can click ‘Set children to tier’ to set all members (recursively) to the tier level of the given group.

**Set tier for GPOs**

If you click ‘Set tier for GPOs’ each GPO will have their tier level set to the tier level of the OU with highest tier (closest to zero) which the GPO is linked to. GPOs not linked to an OU will not have their tier level changed.

**Get tiering violations**

Find all relations in the BloodHound database where an AD object has control of an AD object from a higher tier (closer to zero). 

Two CSV files are generated as output:

* adobjects-[timestamp].csv: All AD objects and which tier they are in.

* tiering-violations-[timestamp].csv: The tiering violations.

Example of records in violations CSV:


|SourceTier	| SourceType	| SourceName	| SourceDistinguishedname	| Relation	| IsInherited	| TargetTier	| TargetType	| TargetName	| TargetDistinguishedname|
|-----------|---------------|---------------|---------------------------|-----------|---------------|---------------|---------------|---------------|------------------------|
|Tier1 | User | svc-monitor@HOT.LOCAL | CN=svc-monitor,CN=Users,DC=hot,DC=local | ForceChangePassword | True | Tier0 | User | T0_JBK@HOT.LOCAL | CN=T0_JBK,CN=Users,DC=hot,DC=local |
|Tier2 | Group | Wrk-Admins@HOT.LOCAL | CN=Wrk-Admins,CN=Groups,DC=hot,DC=local | GenericWrite | | Tier0 | GPO | PowerShellLogging@HOT.LOCAL | CN={6AC1786C-016F-11D2-945F-00C04fB984F9},CN=Policies,CN=System,DC=hot,DC=local |

The first record is a Tier 1 service account with permission to change the password of a Tier 0 user account. The relation is inherited. Unfortunately, it is not always possible to view where the relation is inherited from in the BloodHound data, but you can check it manually by inspecting the permissions on the targeted AD object in Users and Computers. The second record is a group with permission to edit a GPO, which is likely linked to OU containing Tier 0 servers since it is a Tier 0 GPO.

You can look up all the relation types and how they are exploited [here](https://bloodhound.readthedocs.io/en/latest/data-analysis/edges.html).

If you discover that an object is in a too high tier (closest to zero), you should correct it in ImproHound, and then check for violations with this object as SOURCE. If an object is in a too low tier (closest to infinity), you should correct it in ImproHound and check for violations with the object as TARGET.

**Delete tiering**

All tier labels and ImproHound created nodes in the BloodHound database will be deleted.


## Guidelines for tiering AD objects

It is important to tier the AD objects correctly. If you set a DC and a regular low privileged user to be Tier 0 objects, ImproHound will not find that the user's admin access to the DC is a tiering violation. Same case if you add the two of them to Tier 2.

### Computer

Computers are tiered after how critical it would be if the computer was compromised.

* Tier 0 – Domain Controllers and other systems categorized as critical. Systems such as SCCM, ADFS, PKI, and virtualization servers (VMware, Hyper V, etc.)
* Tier 1 – The rest of the server infrastructure
* Tier 2 – Regular workstations

### User

Users are tiered after the computers they can logon to and after the AD objects they have a control over. An example of control permission could be a user with rights to edit GPOs linked to Tier 1 servers, which would make the user a Tier 1 object.

### Group

A group belongs to the lowest tier (closest to infinity) of its members, unless the group have bad members, e.g. a regular user as member of Domain Admins.

Examples: Domain Users is a Tier 2 group even though your Tier 0 users are members of the group because it is not the membership of Domain Users that gives the users privileges. On the other hand, the Domain Admins group is a Tier 0 group because the membership of this group makes users very privileged. _Cloneable Domain Controllers_ does not have any AD privileges AFAIK, but it should only contain Tier 0 objects i.e. DCs, so it is a Tier 0 group.


### Container (incl. OU and Domain)

A container belongs to the highest tier (closest to zero) of its child objects, or higher.

Example: You have all Tier 0, Tier 1, and Tier 2 users in the Users container. A user with Full Control permission on the Users container would be able to compromise all the users including the Tier 0 users (except some which are protected but that is not important for the example), so the Users container must be a Tier 0 object.

### GPO

A GPO’s tier level is determined by the tier level of the OUs it is linked to. The GPO belongs to the highest tier (closest to zero) of the OUs it is linked to.
Use the ‘Set tier for GPOs’ button to make sure all GPOs follow this principle.

Example: A user with permission to edit a GPO linked to a Tier 1 OU would be able to control membership of Administrators on all servers under the Tier 1 OU by modifying the GPO.

## Latest tested version of dependencies
* [BloodHound 4.2.0](https://github.com/BloodHoundAD/BloodHound/releases/tag/4.2.0)
* [Neo4j 4.4.10](https://go.neo4j.com/download-thanks.html?edition=community&release=4.4.10)
* [APOC 4.4.0.8](https://github.com/neo4j-contrib/neo4j-apoc-procedures/releases/tag/4.4.0.8)
* [Java/OpenJDK 11.0.16.1 LTS](https://docs.microsoft.com/en-us/java/openjdk/download#openjdk-110161-lts--see-previous-releases)

## TODO

- Filter out GetChanges / GetChangesAll where only one of them exist.
- More Tier 0 by default: RODC (incl. krbtgt_xxxxxx), MSOL_, AdminSDHolder, etc.  
