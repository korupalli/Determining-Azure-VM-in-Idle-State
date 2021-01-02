# Determining-Azure-VM-in-Idle-State
Using an Azure C# function to find idle virtual machines and send automatic notifications

## Bussiness Case
Virtual Machines can be expensive and often are left running unintentionally. Ideally if there if it is not in use they can be stopped and then restarted when needed.
- You will build a solution that will email someone when:
  - A VM is turned on or Stopped but not Deallocated
  - The VM has had no usage for 36 hours
