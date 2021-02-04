# ImproHound
BloodHound for blue teamers

## How it works

1. Run SharpHound
1. Upload collected data to BloodHound
1. Install APOC
1. Run the script!

### How the tiering is done
In the json file you specify which tier certain AD objects belong to. This is how the program process the different objects:
1. OUs
    1. Set the OU to the given tier.
    1. Set everything with a distinguished name ending with the distinguished name of the OU to the given tier.
1. Groups
    1. Set the group to the given tier.
    1. Set every (recursive) members to the given tier.
1. Principals
    1. Set the principal ending with the given RID to the given tier.

Afterwards, the program performs the following steps:
1. Set all nodes to be in a default tier (Tier 2)			
1. Set all nodes that are in multiple tiers to be only in the lowest one they are set to
1. Replace domain objects (parent to top OUs) tier level with Tier 0
1. Make sure all groups are in the right tier
    1. Replace the current tier level for all groups with the highest tier level of their recursive members
1. Make sure all OUs are in right tier
    1. Replace the current tier level for all OUs with the lowest tier level of the AD objects under the OUs.
1. Make sure all GPOs are in right tier
    1. Replace the current tier level for all GPOs to the lowest by the OUs linked to.

## Dev setup (Windows)

1. Download/clone the project

1. Download and install neo4j Desktop: https://neo4j.com/download/?ref=try-neo4j-lp
    1. Set the 'Data path' under Settings in Neo4j Desktop to the root of the ImproHound repo
    1. Create a new project in Neo4j Desktop
    1. Install APOC plugin in Neo4j Desktop under the new project
    1. Allow APOC load data. Go to settings for your database in Neo4j Desktop and add these lines in the bottom:
        ```
        apoc.import.file.enabled=true
        apoc.import.file.use_neo4j_config=false
        ```
        * Note: use_neo4j_config=false: Allow it to import from anywhere on the PC

1. Download and install Python 3 (incl. pip): https://www.python.org/downloads/windows/

1. Install JupyterLab (from cmd)
    ```
    pip install jupyterlab
    ```

1. Install neo4j library for connecting the jupyter notebook to the Neo4j Server (from cmd)
    ```
    pip install neo4j
    ```

## Dev notes
Start jupyter lab (from cmd)
```
jupyter lab
```
