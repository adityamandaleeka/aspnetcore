// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson.Exceptions;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson.Operations;
using Xunit;

namespace Microsoft.AspNetCore.JsonPatch.SystemTextJson;

public class JsonPatchDocumentJObjectTest
{
    [Fact]
    public void ApplyTo_Array_Add()
    {
        // Arrange
        var model = new ObjectWithJObject { CustomData = (JsonObject)JsonSerializer.SerializeToNode(new { Emails = new[] { "foo@bar.com" } }) };
        var patch = new JsonPatchDocument<ObjectWithJObject>();

        patch.Operations.Add(new Operation<ObjectWithJObject>("add", "/CustomData/Emails/-", null, "foo@baz.com"));

        // Act
        patch.ApplyTo(model);

        // Assert
        Assert.Equal("foo@baz.com", model.CustomData["Emails"][1].GetValue<string>());
    }

    [Fact]
    public void ApplyTo_Model_Test1()
    {
        // Arrange
        var model = new ObjectWithJObject { CustomData = (JsonObject)JsonSerializer.SerializeToNode(new { Email = "foo@bar.com", Name = "Bar" }) };
        var patch = new JsonPatchDocument<ObjectWithJObject>();

        patch.Operations.Add(new Operation<ObjectWithJObject>("test", "/CustomData/Email", null, "foo@baz.com"));
        patch.Operations.Add(new Operation<ObjectWithJObject>("add", "/CustomData/Name", null, "Bar Baz"));

        // Act & Assert
        Assert.Throws<JsonPatchException>(() => patch.ApplyTo(model));
    }

    [Fact]
    public void ApplyTo_Model_Test2()
    {
        // Arrange
        var model = new ObjectWithJObject { CustomData = new JsonObject([new("Email", "foo@bar.com"), new("Name", "Bar")]) };
        var patch = new JsonPatchDocument<ObjectWithJObject>();

        patch.Operations.Add(new Operation<ObjectWithJObject>("test", "/CustomData/Email", null, "foo@bar.com"));
        patch.Operations.Add(new Operation<ObjectWithJObject>("add", "/CustomData/Name", null, "Bar Baz"));

        // Act
        patch.ApplyTo(model);

        // Assert
        Assert.Equal("Bar Baz", model.CustomData["Name"].GetValue<string>());
    }

    [Fact]
    public void ApplyTo_Model_Copy()
    {
        // Arrange
        var model = new ObjectWithJObject { CustomData = new JsonObject([new("Email", "foo@bar.com")]) };

        var patch = new JsonPatchDocument<ObjectWithJObject>();
        patch.Operations.Add(new Operation<ObjectWithJObject>("copy", "/CustomData/UserName", "/CustomData/Email"));

        // Act
        patch.ApplyTo(model);

        // Assert
        Assert.Equal("foo@bar.com", model.CustomData["UserName"].GetValue<string>());
    }

    [Fact]
    public void ApplyTo_Model_Remove()
    {
        // Arrange
        var model = new ObjectWithJObject { CustomData = new JsonObject([new("FirstName", "Bar"), new("LastName", "Bar")]) };
        var patch = new JsonPatchDocument<ObjectWithJObject>();

        patch.Operations.Add(new Operation<ObjectWithJObject>("remove", "/CustomData/LastName", null));

        // Act
        patch.ApplyTo(model);

        // Assert
        Assert.False(model.CustomData.ContainsKey("LastName"));
    }

    [Fact]
    public void ApplyTo_Model_Move()
    {
        // Arrange
        var model = new ObjectWithJObject { CustomData = new JsonObject([new("FirstName", "Bar")]) };
        var patch = new JsonPatchDocument<ObjectWithJObject>();

        patch.Operations.Add(new Operation<ObjectWithJObject>("move", "/CustomData/LastName", "/CustomData/FirstName"));

        // Act
        patch.ApplyTo(model);

        // Assert
        Assert.False(model.CustomData.ContainsKey("FirstName"));
        Assert.Equal("Bar", model.CustomData["LastName"].GetValue<string>());
    }

    [Fact]
    public void ApplyTo_Model_Add()
    {
        // Arrange
        var model = new ObjectWithJObject();
        var patch = new JsonPatchDocument<ObjectWithJObject>();

        patch.Operations.Add(new Operation<ObjectWithJObject>("add", "/CustomData/Name", null, "Foo"));

        // Act
        patch.ApplyTo(model);

        // Assert
        Assert.Equal("Foo", model.CustomData["Name"].GetValue<string>());
    }

    [Fact]
    public void ApplyTo_Model_Add_Null()
    {
        // Arrange
        var model = new ObjectWithJObject();
        var patch = new JsonPatchDocument<ObjectWithJObject>();

        patch.Operations.Add(new Operation<ObjectWithJObject>("add", "/CustomData/Name", null, null));

        // Act
        patch.ApplyTo(model);

        // Assert
        Assert.Contains("Name", model.CustomData);
        Assert.Null(model.CustomData["Name"]);
    }

    [Fact]
    public void ApplyTo_Model_Replace()
    {
        // Arrange
        var model = new ObjectWithJObject { CustomData = new JsonObject([new("Email", "foo@bar.com"), new("Name", "Bar")]) };
        var patch = new JsonPatchDocument<ObjectWithJObject>();

        patch.Operations.Add(new Operation<ObjectWithJObject>("replace", "/CustomData/Email", null, "foo@baz.com"));

        // Act
        patch.ApplyTo(model);

        // Assert
        Assert.Equal("foo@baz.com", model.CustomData["Email"].GetValue<string>());
    }

    [Fact]
    public void ApplyTo_Model_Replace_Null()
    {
        // Arrange
        var model = new ObjectWithJObject { CustomData = new JsonObject([new("Email", "foo@bar.com"), new("Name", "Bar")]) };
        var patch = new JsonPatchDocument<ObjectWithJObject>();

        patch.Operations.Add(new Operation<ObjectWithJObject>("replace", "/CustomData/Email", null, null));

        // Act
        patch.ApplyTo(model);

        // Assert
        Assert.Null(model.CustomData["Email"]);
    }

    [Fact]
    public void ApplyTo_JsonObject_Replace_ArrayItemProperty()
    {
        // Arrange
        var doc = JsonNode.Parse("""
            {
                "items": [
                    { "id": 1, "name": "First" },
                    { "id": 2, "name": "Second" }
                ]
            }
            """)?.AsObject();

        var patch = new JsonPatchDocument();
        patch.Replace("/items/0/name", "Updated");

        // Act
        patch.ApplyTo(doc);

        // Assert
        Assert.Equal("Updated", doc["items"][0]["name"].GetValue<string>());
    }

    [Fact]
    public void ApplyTo_JsonObject_Remove_ArrayItemProperty()
    {
        // Arrange
        var doc = JsonNode.Parse("""
            {
                "items": [
                    { "id": 1, "name": "First", "extra": "remove-me" }
                ]
            }
            """)?.AsObject();

        var patch = new JsonPatchDocument();
        patch.Remove("/items/0/extra");

        // Act
        patch.ApplyTo(doc);

        // Assert
        Assert.False(((JsonObject)doc["items"][0]).ContainsKey("extra"));
    }

    [Fact]
    public void ApplyTo_JsonObject_Add_ArrayItemProperty()
    {
        // Arrange
        var doc = JsonNode.Parse("""
            {
                "items": [
                    { "id": 1 }
                ]
            }
            """)?.AsObject();

        var patch = new JsonPatchDocument();
        patch.Add("/items/0/name", "NewProp");

        // Act
        patch.ApplyTo(doc);

        // Assert
        Assert.Equal("NewProp", doc["items"][0]["name"].GetValue<string>());
    }

    [Fact]
    public void ApplyTo_JsonObject_DeeplyNested_ArrayTraversal()
    {
        // Arrange
        var doc = JsonNode.Parse("""
            {
                "root": {
                    "items": [
                        {
                            "subitems": [
                                { "value": "original" }
                            ]
                        }
                    ]
                }
            }
            """)?.AsObject();

        var patch = new JsonPatchDocument();
        patch.Replace("/root/items/0/subitems/0/value", "updated");

        // Act
        patch.ApplyTo(doc);

        // Assert
        Assert.Equal("updated", doc["root"]["items"][0]["subitems"][0]["value"].GetValue<string>());
    }
}
