# Worksharing Support in RevitTestRunner

The RevitTestRunner provides comprehensive support for testing workshared Revit models with advanced workset management capabilities, detailed logging, and flexible configuration options.

## Overview

Worksharing in Revit allows multiple users to collaborate on the same model by dividing it into worksets. When testing workshared models, you need precise control over:

- How the model is detached from the central file
- Which worksets are opened during testing
- How the test environment is isolated from production

The RevitTestRunner provides this control through the `DetachOption` and `WorksetsToOpen` parameters.

## Quick Start

```csharp
using Xunit;
using RevitTestFramework.Xunit;
using Autodesk.Revit.DB;

public class WorksharingTests
{
    // Recommended approach for most tests
    [RevitFact(@"C:\Models\CentralModel.rvt", 
               DetachOption = DetachOption.DetachAndPreserveWorksets,
               WorksetsToOpen = [1, 2, 5])]
    public void Should_TestSpecificWorksets(Document doc)
    {
        Assert.False(doc.IsWorkshared, "Document should be detached from central");
        
        var worksets = new FilteredWorksetCollector(doc).ToWorksets();
        var openWorksets = worksets.Where(w => w.IsOpen && w.Kind == WorksetKind.UserWorkset).ToList();
        
        Assert.Equal(3, openWorksets.Count);
        var openIds = openWorksets.Select(w => w.Id.IntegerValue).ToArray();
        Assert.Contains(1, openIds);
        Assert.Contains(2, openIds);
        Assert.Contains(5, openIds);
    }
}
```

## DetachOption Parameter

The `DetachOption` parameter controls how the model is detached from its central file.

### DetachOption.DoNotDetach (Default)

```csharp
[RevitFact(@"C:\Models\CentralModel.rvt", DetachOption = DetachOption.DoNotDetach)]
public void TestConnectedToCentral(Document doc)
{
    // Model remains connected to central
    // WARNING: Changes may affect the central model
    // Use with extreme caution in production environments
}
```

**Use Cases:**
- Testing synchronization workflows
- Testing central model operations
- Testing workset ownership functionality

**?? Warning:** This option keeps the model connected to the central file. Any changes made during testing could affect the production model. Use only when absolutely necessary and ensure proper safeguards.

### DetachOption.DetachAndPreserveWorksets (Recommended)

```csharp
[RevitFact(@"C:\Models\CentralModel.rvt", DetachOption = DetachOption.DetachAndPreserveWorksets)]
public void TestDetachedWithWorksets(Document doc)
{
    Assert.False(doc.IsWorkshared, "Document should be detached from central");
    
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    Assert.True(worksets.Count > 0, "Worksets should be preserved");
    
    // Test workset-specific functionality
    foreach (var workset in worksets)
    {
        // Workset information is preserved but not connected to central
        Assert.NotNull(workset.Name);
        Assert.True(workset.Id.IntegerValue > 0);
    }
}
```

**Use Cases:**
- Most worksharing tests (recommended default)
- Testing workset-aware functionality
- Safe isolated testing environment
- Performance testing with worksets

**Benefits:**
- Complete isolation from central model
- Preserves workset structure for testing
- Safe for production environments
- Maintains workset metadata

### DetachOption.DetachAndDiscardWorksets

```csharp
[RevitFact(@"C:\Models\CentralModel.rvt", DetachOption = DetachOption.DetachAndDiscardWorksets)]
public void TestNonWorksharingFunctionality(Document doc)
{
    Assert.False(doc.IsWorkshared, "Document should not be workshared");
    
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    Assert.Empty(worksets, "All worksets should be discarded");
    
    // Test functionality that doesn't depend on worksets
    var elements = new FilteredElementCollector(doc)
        .WhereElementIsNotElementType()
        .ToElements();
    
    Assert.True(elements.Count > 0, "Model content should be preserved");
}
```

**Use Cases:**
- Testing non-worksharing functionality on workshared models
- Performance testing without workset overhead
- Converting workshared models to non-workshared for testing
- Testing model behavior after worksharing removal

### DetachOption.ClearTransmittedSaveAsNewCentral

```csharp
[RevitFact(@"C:\Models\TransmittedModel.rvt", 
           DetachOption = DetachOption.ClearTransmittedSaveAsNewCentral)]
public void TestNewCentralCreation(Document doc)
{
    // Creates a new central model from transmitted file
    // Useful for testing transmitted model workflows
}
```

**Use Cases:**
- Testing transmitted model workflows
- Creating new central models from transmitted files
- Testing model preparation processes

## WorksetsToOpen Parameter

The `WorksetsToOpen` parameter specifies which worksets should be opened by their workset ID.

### Opening Specific Worksets

```csharp
[RevitFact(@"C:\Models\WorksharedModel.rvt", WorksetsToOpen = [1, 2, 5])]
public void TestSpecificWorksets(Document doc)
{
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    var openWorksets = worksets.Where(w => w.IsOpen).ToList();
    var closedWorksets = worksets.Where(w => !w.IsOpen).ToList();
    
    // Verify specific worksets are open
    var openUserWorksets = openWorksets.Where(w => w.Kind == WorksetKind.UserWorkset).ToList();
    var expectedIds = new[] { 1, 2, 5 };
    var actualIds = openUserWorksets.Select(w => w.Id.IntegerValue).ToArray();
    
    Assert.Equal(expectedIds.Length, actualIds.Length);
    foreach (var expectedId in expectedIds)
    {
        Assert.Contains(expectedId, actualIds);
    }
    
    // Verify other worksets are closed
    Assert.True(closedWorksets.Count > 0, "Some worksets should be closed");
}
```

### Single Workset Testing

```csharp
[RevitFact(@"C:\Models\ArchitectureModel.rvt", WorksetsToOpen = [1])]
public void TestArchitectureWorksetOnly(Document doc)
{
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    var openUserWorksets = worksets
        .Where(w => w.IsOpen && w.Kind == WorksetKind.UserWorkset)
        .ToList();
    
    Assert.Single(openUserWorksets, "Only one user workset should be open");
    Assert.Equal(1, openUserWorksets.First().Id.IntegerValue);
    
    // Test functionality specific to architecture workset
    var archElements = new FilteredElementCollector(doc)
        .WhereElementIsNotElementType()
        .Where(e => e.WorksetId.IntegerValue == 1)
        .ToElements();
    
    Assert.True(archElements.Count > 0, "Architecture workset should contain elements");
}
```

### Performance Testing with Large Models

```csharp
[RevitFact(@"C:\Models\LargeModel.rvt", 
           DetachOption = DetachOption.DetachAndPreserveWorksets,
           WorksetsToOpen = [1])] // Open only essential workset
public void TestPerformanceWithMinimalWorksets(Document doc)
{
    var stopwatch = Stopwatch.StartNew();
    
    // Perform operations on minimal workset data
    var elements = new FilteredElementCollector(doc)
        .WhereElementIsNotElementType()
        .OfClass(typeof(Wall))
        .ToElements();
    
    stopwatch.Stop();
    
    // Assert performance meets requirements
    Assert.True(stopwatch.ElapsedMilliseconds < 5000, 
                $"Operation took {stopwatch.ElapsedMilliseconds}ms, expected < 5000ms");
}
```

## Combined Usage Examples

### Complete Worksharing Test Suite

```csharp
public class ComprehensiveWorksharingTests
{
    [RevitFact(@"C:\Models\CentralModel.rvt", 
               DetachOption = DetachOption.DetachAndPreserveWorksets,
               WorksetsToOpen = [1, 2])]
    public void Should_IsolateArchitectureAndStructure_ForTesting(Document doc)
    {
        // Verify detachment
        Assert.False(doc.IsWorkshared, "Model should be detached from central");
        
        // Verify workset configuration
        var worksets = new FilteredWorksetCollector(doc).ToWorksets();
        var openWorksets = worksets.Where(w => w.IsOpen && w.Kind == WorksetKind.UserWorkset).ToList();
        var closedWorksets = worksets.Where(w => !w.IsOpen && w.Kind == WorksetKind.UserWorkset).ToList();
        
        Assert.Equal(2, openWorksets.Count);
        Assert.True(closedWorksets.Count > 0);
        
        // Verify specific worksets
        var openIds = openWorksets.Select(w => w.Id.IntegerValue).ToArray();
        Assert.Contains(1, openIds); // Architecture
        Assert.Contains(2, openIds); // Structure
        
        // Test element visibility and access
        var archElements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(e => e.WorksetId.IntegerValue == 1)
            .ToElements();
            
        var structElements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(e => e.WorksetId.IntegerValue == 2)
            .ToElements();
        
        Assert.True(archElements.Count > 0, "Architecture elements should be accessible");
        Assert.True(structElements.Count > 0, "Structure elements should be accessible");
    }
    
    [RevitFact(@"C:\Models\CentralModel.rvt", 
               DetachOption = DetachOption.DetachAndPreserveWorksets,
               WorksetsToOpen = [3])] // MEP workset only
    public void Should_TestMEPWorkset_InIsolation(Document doc)
    {
        var worksets = new FilteredWorksetCollector(doc).ToWorksets();
        var mepWorkset = worksets.FirstOrDefault(w => w.Id.IntegerValue == 3);
        
        Assert.NotNull(mepWorkset);
        Assert.True(mepWorkset.IsOpen, "MEP workset should be open");
        Assert.Equal("MEP", mepWorkset.Name); // Assuming workset name
        
        // Test MEP-specific functionality
        var mepElements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(e => e.WorksetId.IntegerValue == 3)
            .OfCategory(BuiltInCategory.OST_DuctCurves)
            .ToElements();
        
        // Verify MEP elements are accessible
        foreach (var element in mepElements)
        {
            Assert.Equal(3, element.WorksetId.IntegerValue);
            Assert.True(element.IsValidObject);
        }
    }
    
    [RevitFact(@"C:\Models\CentralModel.rvt", 
               DetachOption = DetachOption.DetachAndDiscardWorksets)]
    public void Should_TestModelBehavior_WithoutWorksharing(Document doc)
    {
        Assert.False(doc.IsWorkshared, "Document should not be workshared");
        
        var worksets = new FilteredWorksetCollector(doc).ToWorksets();
        Assert.Empty(worksets, "No worksets should exist");
        
        // Test that all elements are accessible without workset restrictions
        var allElements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElements();
        
        Assert.True(allElements.Count > 0, "All model elements should be accessible");
        
        // Verify no workset restrictions
        foreach (var element in allElements.Take(10)) // Sample check
        {
            // In non-workshared model, WorksetId should be default/invalid
            Assert.True(element.IsValidObject);
        }
    }
}
```

### Cloud Model Worksharing

```csharp
[RevitFact("project-guid", "model-guid", 
           DetachOption = DetachOption.DetachAndPreserveWorksets,
           WorksetsToOpen = [311, 312])]
public void Should_TestCloudModel_WithSpecificWorksets(Document doc)
{
    Assert.NotNull(doc);
    Assert.False(doc.IsWorkshared, "Cloud model should be detached");
    
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    var openWorksets = worksets.Where(w => w.IsOpen && w.Kind == WorksetKind.UserWorkset).ToList();
    
    var openIds = openWorksets.Select(w => w.Id.IntegerValue).ToArray();
    Assert.Contains(311, openIds);
    Assert.Contains(312, openIds);
    
    // Test cloud model specific functionality
    // Cloud models are automatically detached so IsWorkshared will be false
}
```

## Default Behavior

### When No Options Are Specified

```csharp
[RevitFact(@"C:\Models\WorksharedModel.rvt")]
public void TestDefaultBehavior(Document doc)
{
    // Default behavior:
    // - DetachOption: DoNotDetach (remains connected to central)
    // - WorksetsToOpen: null (all worksets closed for performance)
    
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    var openUserWorksets = worksets.Where(w => w.IsOpen && w.Kind == WorksetKind.UserWorkset).ToList();
    
    Assert.Empty(openUserWorksets, "User worksets should be closed by default");
}
```

### Performance Optimization

The default behavior closes all worksets to optimize performance:
- Faster model loading
- Reduced memory usage
- Better test execution speed
- Consistent test environment

## Comprehensive Logging

The RevitTestRunner provides detailed logging of all worksharing operations to help with debugging and monitoring:

### Model Opening Logs

```
2024-01-15 14:30:25.123 [INFO] === RevitTestModelHelper.OpenModel - START ===
2024-01-15 14:30:25.125 [INFO] Configuration: LocalPath=C:\Models\Project.rvt
2024-01-15 14:30:25.127 [INFO] DetachFromCentral: DetachAndPreserveWorksets
2024-01-15 14:30:25.129 [INFO] WorksetsToOpen: [1, 2, 5]
2024-01-15 14:30:25.131 [INFO] Opening local model...
2024-01-15 14:30:25.133 [INFO] Opening with DetachFromCentralOption: DetachAndPreserveWorksets
2024-01-15 14:30:25.135 [INFO] Opening local model with 3 specified worksets: [1, 2, 5]
2024-01-15 14:30:25.137 [INFO] Opening local model: C:\Models\Project.rvt
2024-01-15 14:30:27.245 [INFO] Successfully opened local model: Project
```

### Model Information Logs

```
2024-01-15 14:30:27.247 [INFO] --- Model Information ---
2024-01-15 14:30:27.249 [INFO] Document Title: Project
2024-01-15 14:30:27.251 [INFO] Document Path: C:\Models\Project.rvt
2024-01-15 14:30:27.253 [INFO] Is Workshared: False
2024-01-15 14:30:27.255 [INFO] Is Family Document: False
2024-01-15 14:30:27.257 [INFO] Application Version: Autodesk Revit 2025 (2025)
```

### Detailed Workset Information

```
2024-01-15 14:30:27.259 [INFO] --- Workset Information ---
2024-01-15 14:30:27.261 [INFO] Central Model Path: \\server\central\Project_Central.rvt
2024-01-15 14:30:27.263 [INFO] Total Worksets: 8
2024-01-15 14:30:27.265 [INFO] Workset Details:
2024-01-15 14:30:27.267 [INFO]   ID: 1, Name: 'Architecture', Status: OPEN, EDITABLE, Owner: user1
2024-01-15 14:30:27.269 [INFO]   ID: 2, Name: 'Structure', Status: OPEN, EDITABLE, Owner: user2
2024-01-15 14:30:27.271 [INFO]   ID: 5, Name: 'Interiors', Status: OPEN, EDITABLE, Owner: user3
2024-01-15 14:30:27.273 [INFO]   ID: 3, Name: 'MEP', Status: CLOSED, READ-ONLY, Owner: No Owner
2024-01-15 14:30:27.275 [INFO]   ID: 4, Name: 'Site', Status: CLOSED, READ-ONLY, Owner: No Owner
2024-01-15 14:30:27.277 [INFO]   ID: 6, Name: 'Consultants', Status: CLOSED, READ-ONLY, Owner: No Owner
2024-01-15 14:30:27.279 [INFO]   ID: 7, Name: 'Links', Status: CLOSED, READ-ONLY, Owner: No Owner
2024-01-15 14:30:27.281 [INFO]   ID: 8, Name: 'Shared Levels and Grids', Status: CLOSED, READ-ONLY, Owner: No Owner
```

### Summary Information

```
2024-01-15 14:30:27.283 [INFO] Open Worksets: 3
2024-01-15 14:30:27.285 [INFO]   Open IDs: [1, 2, 5]
2024-01-15 14:30:27.287 [INFO]   Open Names: ['Architecture', 'Structure', 'Interiors']
2024-01-15 14:30:27.289 [INFO] Closed Worksets: 5
2024-01-15 14:30:27.291 [INFO]   Closed IDs: [3, 4, 6, 7, 8]
2024-01-15 14:30:27.293 [INFO]   Closed Names: ['MEP', 'Site', 'Consultants', 'Links', 'Shared Levels and Grids']
2024-01-15 14:30:27.295 [INFO] --- End Workset Information ---
2024-01-15 14:30:27.297 [INFO] --- End Model Information ---
2024-01-15 14:30:27.299 [INFO] === RevitTestModelHelper.OpenModel - END (Success) ===
```

### Log File Location

Log files are stored at: `%LOCALAPPDATA%\RevitTestRunner\Logs\RevitTestFramework.Common-yyyyMMdd.log`

## Best Practices

### 1. Use DetachAndPreserveWorksets for Most Tests

```csharp
[RevitFact(@"C:\Models\CentralModel.rvt", 
           DetachOption = DetachOption.DetachAndPreserveWorksets)]
public void RecommendedApproach(Document doc)
{
    // Safe, isolated, preserves workset structure
}
```

### 2. Explicitly Specify WorksetsToOpen

```csharp
[RevitFact(@"C:\Models\Model.rvt", WorksetsToOpen = [1, 2])]
public void ExplicitWorksetControl(Document doc)
{
    // Ensures consistent test conditions
    // Improves performance by loading only necessary worksets
}
```

### 3. Test Different Workset Combinations

```csharp
public class WorksetCombinationTests
{
    [RevitFact(@"C:\Models\Model.rvt", WorksetsToOpen = [1])]
    public void TestArchitectureOnly(Document doc) { }
    
    [RevitFact(@"C:\Models\Model.rvt", WorksetsToOpen = [2])]
    public void TestStructureOnly(Document doc) { }
    
    [RevitFact(@"C:\Models\Model.rvt", WorksetsToOpen = [1, 2])]
    public void TestArchitectureAndStructure(Document doc) { }
}
```

### 4. Use Comprehensive Assertions

```csharp
[RevitFact(@"C:\Models\Model.rvt", 
           DetachOption = DetachOption.DetachAndPreserveWorksets,
           WorksetsToOpen = [1, 2])]
public void ComprehensiveWorksetValidation(Document doc)
{
    // Verify detachment status
    Assert.False(doc.IsWorkshared, "Should be detached");
    
    // Verify workset states
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    var openWorksets = worksets.Where(w => w.IsOpen && w.Kind == WorksetKind.UserWorkset).ToList();
    var closedWorksets = worksets.Where(w => !w.IsOpen && w.Kind == WorksetKind.UserWorkset).ToList();
    
    // Verify counts
    Assert.Equal(2, openWorksets.Count);
    Assert.True(closedWorksets.Count > 0);
    
    // Verify specific worksets
    var openIds = openWorksets.Select(w => w.Id.IntegerValue).ToArray();
    Assert.Contains(1, openIds);
    Assert.Contains(2, openIds);
    
    // Verify element access
    foreach (var worksetId in new[] { 1, 2 })
    {
        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(e => e.WorksetId.IntegerValue == worksetId)
            .ToElements();
        
        Assert.True(elements.Count >= 0, $"Should be able to access workset {worksetId} elements");
    }
}
```

### 5. Check Log Files for Debugging

When tests fail or behave unexpectedly:

1. Check the log files at `%LOCALAPPDATA%\RevitTestRunner\Logs\`
2. Look for workset information in the logs
3. Verify that worksets are opening as expected
4. Check for any error messages during model opening

### 6. Performance Considerations

- Open only the worksets you need for testing
- Use `DetachAndPreserveWorksets` to avoid central model overhead
- Consider using `DetachAndDiscardWorksets` for non-worksharing tests on workshared models
- Monitor log files for model opening performance

## Common Scenarios

### Testing Workset-Specific Functionality

```csharp
[RevitFact(@"C:\Models\ArchModel.rvt", 
           DetachOption = DetachOption.DetachAndPreserveWorksets,
           WorksetsToOpen = [1])] // Architecture workset
public void TestArchitecturalElements(Document doc)
{
    var walls = new FilteredElementCollector(doc)
        .WhereElementIsNotElementType()
        .OfClass(typeof(Wall))
        .Where(w => w.WorksetId.IntegerValue == 1)
        .ToElements();
    
    Assert.True(walls.Count > 0, "Architecture workset should contain walls");
}
```

### Testing Cross-Workset Functionality

```csharp
[RevitFact(@"C:\Models\Model.rvt", 
           DetachOption = DetachOption.DetachAndPreserveWorksets,
           WorksetsToOpen = [1, 2])] // Architecture and Structure
public void TestArchitectureStructureInteraction(Document doc)
{
    var archWalls = new FilteredElementCollector(doc)
        .WhereElementIsNotElementType()
        .OfClass(typeof(Wall))
        .Where(w => w.WorksetId.IntegerValue == 1)
        .ToElements();
    
    var structColumns = new FilteredElementCollector(doc)
        .WhereElementIsNotElementType()
        .OfClass(typeof(FamilyInstance))
        .Where(f => f.WorksetId.IntegerValue == 2)
        .Cast<FamilyInstance>()
        .Where(f => f.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralColumns)
        .ToElements();
    
    // Test interaction between architectural and structural elements
    Assert.True(archWalls.Count > 0, "Should have architectural walls");
    Assert.True(structColumns.Count > 0, "Should have structural columns");
}
```

### Testing Model Conversion

```csharp
[RevitFact(@"C:\Models\WorksharedModel.rvt", 
           DetachOption = DetachOption.DetachAndDiscardWorksets)]
public void TestConversionToNonWorkshared(Document doc)
{
    Assert.False(doc.IsWorkshared, "Should not be workshared");
    
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    Assert.Empty(worksets, "All worksets should be removed");
    
    // Test that model functionality works without worksets
    var allElements = new FilteredElementCollector(doc)
        .WhereElementIsNotElementType()
        .ToElements();
    
    Assert.True(allElements.Count > 0, "Model content should be preserved");
}
```

## Troubleshooting

### Common Issues

#### Workset Not Found
```
Error: Workset with ID 5 not found in model
```
**Solution:** Check the model's workset structure. Use log files to see available workset IDs.

#### Model Not Detaching
```
Warning: Model remains connected to central despite DetachOption setting
```
**Solution:** Ensure the model file is actually a local file, not a central file directly.

#### Performance Issues
```
Test timeout due to slow model loading
```
**Solution:** Reduce the number of worksets opened, or use `DetachAndDiscardWorksets` for non-worksharing tests.

### Debugging Steps

1. **Check Log Files** - Look at `%LOCALAPPDATA%\RevitTestRunner\Logs\` for detailed information
2. **Verify Workset IDs** - Use Revit UI to check actual workset IDs in your model
3. **Test Incrementally** - Start with one workset, then add more
4. **Check Model Type** - Ensure you're using the correct model (local vs. central vs. cloud)
5. **Validate Paths** - Ensure model paths are correct and accessible

This comprehensive worksharing support makes the RevitTestRunner ideal for testing complex workshared projects with full control over the testing environment.