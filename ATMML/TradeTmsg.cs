
namespace tmsg
{
     public class IdeaIdType {
        
        private int bloombergIdField;
        
        private bool bloombergIdFieldSpecified;
        
        private string thirdPartyIdField;
        
        /// <remarks/>
        public int BloombergId {
            get {
                return this.bloombergIdField;
            }
            set {
                this.bloombergIdField = value;
            }
        }
        
        public bool BloombergIdSpecified {
            get {
                return this.bloombergIdFieldSpecified;
            }
            set {
                this.bloombergIdFieldSpecified = value;
            }
        }
        
        /// <remarks/>
        public string ThirdPartyId {
            get {
                return this.thirdPartyIdField;
            }
            set {
                this.thirdPartyIdField = value;
            }
        }
    }

       public class IdeaInstrumentType {
        
        private object itemField;
        
        public object Item {
            get {
                return this.itemField;
            }
            set {
                this.itemField = value;
            }
        }
    }
}
