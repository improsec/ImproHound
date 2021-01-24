# ImproHound
BloodHound for blue teamers

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
