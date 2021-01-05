# Determining-Azure-VM-in-Idle-State
Using an Azure C# function to find idle virtual machines and send automatic notifications

## Bussiness Case
Virtual Machines can be expensive and often are left running unintentionally. Ideally if there if it is not in use they can be stopped and then restarted when needed.
- You will build a solution that will email someone when:
  - A VM is existing and is in running stage
  - The VM has had no usage for 36 hours

## Solution

- The parameters to be used include CPU usage, Disk Read and write operations and processor idle time.These params are considered because the conditions can be generalised over different types of VMs. Using disk reads and writes can eliminate the effect of varying memory sizes.  
- Thresholds for Parameters    
          - CPU usage  - 2.5 %    
          - Disk Reads - 30    
          - Disk writes - 1    
          - Idle Time - 97 %    
These are the values I determined after testing different situations and referring through some articles
- All the parameters are considered as averages over each hour
- A VM is considered active if any one of CPU usage, Disk reads or writes are greater than their thresholds at least with an Idle time less than its threshold, then the VM is considered active
There will be idle activities of the OS like checkings, updating, installations which happen for a shorter amount of time and result spikes in a trend graph. Using the above metric can eliminate these situations and improve detection accuracy
- If there is a VM that is important and needs to be in a running state irrespective of activity, a specific tag can be collected and depending on that, the VM can be ignored
- Once a VM's are detected, an email notification is sent

### Steps for setting up stack
- Register OperationalInsights in resource providers and Create a loganalytics workspace and configure the logs that you want to track 
- Install the extension on the VM you want to track by connecting to the VM in loganalytics portal
- Create a serice principle and add the ID's to KeyVault
- Create a Function Service and provide give access to keyVault and add the necessary varible to config settings(Code works for local VS code, change if you want to deploy)
- Create a logic app service with HTTP trigger and add the JSON of your requirement and in action, use gmail sothat when ever the http request is called, an action to send email is created. Collect the http link from logicapp designer and replace it in the code along with your json request

Inputs needed in the code
- clientId
- clientSecret
- tenentID or domain
- subscriptionID
- recource groupName
- loganalytics workspaceId

